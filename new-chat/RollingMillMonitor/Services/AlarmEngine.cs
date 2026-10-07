using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;
using RollerMillMonitor.Hubs;
using RollerMillMonitor.Models;
using RollerMillMonitor.Validation;

namespace RollerMillMonitor.Services;

/// <summary>单类型报警状态机（内存）</summary>
public class AlarmTypeState
{
    public bool Active { get; set; }
    public DateTime? PendingSince { get; set; }
    public DateTime? RecoveryPendingSince { get; set; }
}

/// <summary>单测点全部报警类型状态</summary>
public class PointAlarmState
{
    public Dictionary<string, AlarmTypeState> Types { get; } = new();

    public AlarmTypeState For(string type)
    {
        if (!Types.TryGetValue(type, out var s))
        {
            s = new AlarmTypeState();
            Types[type] = s;
        }
        return s;
    }
}

/// <summary>
/// 报警引擎：高高/高/低/低低/通信报警。
/// 支持报警延时（AlarmDelayMs）、恢复延时（RecoveryDelayMs）与死区（Deadband），
/// 避免数值在阈值附近短暂波动时频繁报警。
/// </summary>
public class AlarmEngine
{
    private readonly ConcurrentDictionary<int, PointAlarmState> _states = new();
    private readonly ILogger<AlarmEngine> _logger;
    private readonly IHubContext<MonitorHub> _hub;

    public AlarmEngine(ILogger<AlarmEngine> logger, IHubContext<MonitorHub> hub)
    {
        _logger = logger;
        _hub = hub;
    }

    /// <summary>
    /// 按测点当前值/状态评估一次报警。
    /// </summary>
    public async Task EvaluatePointAsync(
        AppDbContext db, Point point, string runtimeStatus, double? value, CancellationToken ct)
    {
        // 未启用报警的测点不参与报警判断
        if (!point.AlarmEnabled)
            return;

        var state = _states.GetOrAdd(point.Id, _ => new PointAlarmState());
        var now = DateTime.Now;
        var valueValid = value.HasValue && runtimeStatus == PointStatuses.Normal;

        if (DataTypeRegistry.IsBool(point.DataType))
        {
            // 开关量状态测点（油流/油压等）：1 = 正常，0 = 故障 → 触发故障报警
            // 故障报警同样支持报警延时（AlarmDelayMs）与恢复延时（RecoveryDelayMs），
            // 避免开关量在 0/1 间短暂抖动时频繁报警。
            var faultCondition = valueValid && value!.Value == 0;
            await EvalConditionAsync(db, point, state, AlarmTypes.Fault, faultCondition, value, null, now, ct);
        }
        else
        {
            await EvalNumericAsync(db, point, state, AlarmTypes.HighHigh, point.HighHigh, valueValid, value, now, ct);
            await EvalNumericAsync(db, point, state, AlarmTypes.High, point.High, valueValid, value, now, ct);
            await EvalNumericAsync(db, point, state, AlarmTypes.Low, point.Low, valueValid, value, now, ct);
            await EvalNumericAsync(db, point, state, AlarmTypes.LowLow, point.LowLow, valueValid, value, now, ct);
        }

        // 通信报警：PLC 连接失败或读取失败（所有类型测点都评估）
        var commCondition = runtimeStatus is PointStatuses.CommFault or PointStatuses.ReadFailed;
        await EvalConditionAsync(db, point, state, AlarmTypes.Comm, commCondition, null, null, now, ct);
    }

    private async Task EvalNumericAsync(
        AppDbContext db, Point point, PointAlarmState state, string type,
        double? threshold, bool valueValid, double? value, DateTime now, CancellationToken ct)
    {
        if (threshold is null)
            return;

        var condition = valueValid && IsNumericConditionMet(type, value!.Value, threshold.Value, point.Deadband, state.For(type).Active);
        await EvalConditionAsync(db, point, state, type, condition, value, threshold, now, ct);
    }

    /// <summary>数值条件（含死区迟滞）：报警触发用原始阈值，恢复需越过阈值 ± 死区</summary>
    private static bool IsNumericConditionMet(string type, double v, double threshold, double deadband, bool active)
    {
        var isHigh = type is AlarmTypes.High or AlarmTypes.HighHigh;
        if (active)
            return isHigh ? v > threshold - deadband : v < threshold + deadband;
        return isHigh ? v > threshold : v < threshold;
    }

    private async Task EvalConditionAsync(
        AppDbContext db, Point point, PointAlarmState state, string type,
        bool condition, double? value, double? threshold, DateTime now, CancellationToken ct)
    {
        var ts = state.For(type);

        if (condition && !ts.Active)
        {
            // 报警延时
            if (ts.PendingSince is null)
            {
                ts.PendingSince = now;
            }
            else if ((now - ts.PendingSince.Value).TotalMilliseconds >= point.AlarmDelayMs)
            {
                await ActivateAsync(db, point, type, value, threshold, now, ct);
                ts.Active = true;
                ts.PendingSince = null;
                ts.RecoveryPendingSince = null;
            }
        }
        else if (!condition && ts.Active)
        {
            // 恢复延时
            if (ts.RecoveryPendingSince is null)
            {
                ts.RecoveryPendingSince = now;
            }
            else if ((now - ts.RecoveryPendingSince.Value).TotalMilliseconds >= point.RecoveryDelayMs)
            {
                await RecoverAsync(db, point, type, now, ct);
                ts.Active = false;
                ts.RecoveryPendingSince = null;
                ts.PendingSince = null;
            }
        }
        else
        {
            // 条件持续成立 → 重置恢复计时；条件持续不成立 → 重置报警计时
            if (condition && ts.Active) ts.RecoveryPendingSince = null;
            if (!condition && !ts.Active) ts.PendingSince = null;
        }
    }

    private async Task ActivateAsync(
        AppDbContext db, Point point, string type, double? value, double? threshold, DateTime now, CancellationToken ct)
    {
        // 已存在活动报警则不再重复产生
        var existing = await db.Alarms.FirstOrDefaultAsync(a =>
            a.PointId == point.Id && a.AlarmType == type && a.IsActive, ct);
        if (existing is not null)
            return;

        var message = BuildMessage(point, type, value, threshold);
        var record = new AlarmRecord
        {
            PointId = point.Id,
            DeviceId = point.DeviceId,
            PlcId = point.PlcId,
            AlarmType = type,
            AlarmLevel = AlarmTypes.LevelOf(type),
            PointName = point.Name,
            DeviceName = point.Device?.Name ?? "",
            Message = message,
            AlarmValue = value,
            Threshold = threshold,
            AlarmTime = now,
            IsActive = true
        };

        db.Alarms.Add(record);
        await db.SaveChangesAsync(ct);

        _logger.LogWarning("报警产生：测点[{point}] 类型[{type}] 值[{value}] 阈值[{threshold}]",
            point.Name, AlarmTypes.DisplayName(type), value, threshold);

        await _hub.Clients.All.SendAsync("AlarmRaised", AlarmDto.From(record, point.Unit), ct);
    }

    private async Task RecoverAsync(AppDbContext db, Point point, string type, DateTime now, CancellationToken ct)
    {
        var active = await db.Alarms.FirstOrDefaultAsync(a =>
            a.PointId == point.Id && a.AlarmType == type && a.IsActive, ct);
        if (active is null)
            return;

        active.IsActive = false;
        active.RecoveryTime = now;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("报警恢复：测点[{point}] 类型[{type}]", point.Name, AlarmTypes.DisplayName(type));

        await _hub.Clients.All.SendAsync("AlarmRecovered", AlarmDto.From(active, point.Unit), ct);
    }

    private static string BuildMessage(Point point, string type, double? value, double? threshold)
    {
        var unit = point.Unit;
        var name = point.Name;
        return type switch
        {
            AlarmTypes.HighHigh => $"{name}高高报警：当前 {Fmt(value)}{unit}，超过阈值 {Fmt(threshold)}{unit}",
            AlarmTypes.High => $"{name}高报警：当前 {Fmt(value)}{unit}，超过阈值 {Fmt(threshold)}{unit}",
            AlarmTypes.Low => $"{name}过低：当前 {Fmt(value)}{unit}，低于阈值 {Fmt(threshold)}{unit}",
            AlarmTypes.LowLow => $"{name}严重过低：当前 {Fmt(value)}{unit}，低于阈值 {Fmt(threshold)}{unit}",
            AlarmTypes.Comm => $"{name}通信报警：测点通信故障或读取失败",
            AlarmTypes.Fault => $"{name}故障：当前状态 {Fmt(value)}（1=正常，0=故障）",
            _ => $"{name}报警"
        };
    }

    private static string Fmt(double? v) => v.HasValue ? v.Value.ToString("0.###") : "-";
}

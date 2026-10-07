using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;
using RollerMillMonitor.Models;

namespace RollerMillMonitor.Services;

/// <summary>
/// 总览数据构建：结合数据库配置与实时缓存，生成设备/测点状态快照。
/// 状态判定遵循"配置决定是否配置、连接与读取决定通信状态"原则。
/// </summary>
public class OverviewService
{
    private readonly RealTimeCache _cache;

    public OverviewService(RealTimeCache cache)
    {
        _cache = cache;
    }

    public async Task<OverviewDto> BuildAsync(AppDbContext db, CancellationToken ct = default)
    {
        var plcs = await db.Plcs.AsNoTracking().ToListAsync(ct);
        var plcMap = plcs.ToDictionary(p => p.Id);

        var devices = await db.Devices.AsNoTracking()
            .Include(d => d.Points)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Id)
            .ToListAsync(ct);

        var activeAlarms = await db.Alarms.AsNoTracking()
            .Where(a => a.IsActive)
            .OrderByDescending(a => a.AlarmTime)
            .Take(100)
            .ToListAsync(ct);

        var alarmByPoint = activeAlarms
            .GroupBy(a => a.PointId)
            .ToDictionary(g => g.Key,
                g => g.OrderByDescending(a => AlarmLevelRank(a.AlarmLevel)).First().AlarmLevel);

        var counts = new OverviewCounts();
        var result = new OverviewDto();

        foreach (var d in devices)
        {
            var od = new OverviewDevice
            {
                Id = d.Id,
                Name = d.Name,
                Code = d.Code,
                Description = d.Description,
                Enabled = d.Enabled,
                SortOrder = d.SortOrder
            };

            var statusPriority = new List<(int rank, string status, string name)>();

            foreach (var p in d.Points.OrderBy(p => p.SortOrder).ThenBy(p => p.Id))
            {
                plcMap.TryGetValue(p.PlcId ?? -1, out var plc);
                var rt = _cache.Get(p.Id);

                var (status, reason) = PointStatusResolver.ResolveFull(p, plc, d.Enabled, rt);

                var op = new OverviewPoint
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    Unit = p.Unit,
                    DataType = p.DataType,
                    Address = p.Address,
                    PlcId = p.PlcId,
                    Value = rt?.Value,
                    Status = status,
                    StatusName = PointStatuses.DisplayName(status),
                    StatusReason = reason,
                    Timestamp = rt?.Timestamp,
                    IsSimulated = plc?.IsSimulated == true && rt is { IsSimulated: true },
                    ActiveAlarmLevel = alarmByPoint.TryGetValue(p.Id, out var lv) ? lv : null
                };
                od.Points.Add(op);

                Count(counts, status);
                statusPriority.Add((RankOf(status), status, PointStatuses.DisplayName(status)));

                if (rt?.Timestamp is not null && (od.LastUpdate is null || rt.Timestamp > od.LastUpdate))
                    od.LastUpdate = rt.Timestamp;
            }

            var worst = statusPriority.OrderBy(x => x.rank).FirstOrDefault();
            od.CommStatus = worst.status ?? PointStatuses.Disabled;
            od.CommStatusName = worst.name ?? "已禁用";
            result.Devices.Add(od);
        }

        result.Plcs = plcs.Select(p => new OverviewPlc
        {
            Id = p.Id,
            Name = p.Name,
            Protocol = p.Protocol,
            IpAddress = p.IpAddress,
            Port = p.Port,
            Enabled = p.Enabled,
            IsSimulated = p.IsSimulated,
            ConnectionStatus = p.ConnectionStatus,
            LastConnectedAt = p.LastConnectedAt,
            LastError = p.LastError
        }).ToList();

        counts.Devices = devices.Count;
        counts.Points = devices.Sum(d => d.Points.Count);
        counts.ActiveAlarms = activeAlarms.Count(a => a.IsActive);

        result.Counts = counts;
        result.ActiveAlarms = activeAlarms.Select(a => AlarmDto.From(a)).ToList();
        result.UpdatedAt = DateTime.Now;
        return result;
    }

    private static void Count(OverviewCounts c, string status)
    {
        switch (status)
        {
            case PointStatuses.Normal: c.Normal++; break;
            case "Waiting": c.Waiting++; break;
            case PointStatuses.NotConfigured: c.NotConfigured++; break;
            case PointStatuses.ConfigError: c.ConfigError++; break;
            case PointStatuses.CommFault: c.CommFault++; break;
            case PointStatuses.ReadFailed: c.ReadFailed++; break;
            case PointStatuses.Disabled: c.Disabled++; break;
        }
    }

    /// <summary>设备综合状态排序：越靠前越严重</summary>
    private static int RankOf(string status) => status switch
    {
        PointStatuses.CommFault => 1,
        PointStatuses.ReadFailed => 2,
        PointStatuses.ConfigError => 3,
        PointStatuses.NotConfigured => 4,
        "Waiting" => 5,
        PointStatuses.Disabled => 6,
        PointStatuses.Normal => 7,
        _ => 8
    };

    private static int AlarmLevelRank(string level) =>
        level == AlarmTypes.LevelCritical ? 2 : 1;
}

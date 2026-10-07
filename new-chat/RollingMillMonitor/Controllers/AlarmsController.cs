using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Api;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;
using RollerMillMonitor.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace RollerMillMonitor.Controllers;

[ApiController]
[Route("api/alarms")]
public class AlarmsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<MonitorHub> _hub;
    private readonly ILogger<AlarmsController> _logger;

    public AlarmsController(AppDbContext db, IHubContext<MonitorHub> hub, ILogger<AlarmsController> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    /// <summary>报警列表，支持按设备/测点/类型/时间范围/确认/恢复状态筛选</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? deviceId,
        [FromQuery] int? pointId,
        [FromQuery] string? alarmType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isAcknowledged,
        [FromQuery] bool? isRecovered,
        [FromQuery] int take = 500)
    {
        var q = _db.Alarms.AsNoTracking().AsQueryable();

        if (deviceId is not null) q = q.Where(a => a.DeviceId == deviceId);
        if (pointId is not null) q = q.Where(a => a.PointId == pointId);
        if (!string.IsNullOrWhiteSpace(alarmType)) q = q.Where(a => a.AlarmType == alarmType.Trim());
        if (from is not null) q = q.Where(a => a.AlarmTime >= from);
        if (to is not null) q = q.Where(a => a.AlarmTime <= to);
        if (isActive is not null) q = q.Where(a => a.IsActive == isActive);
        if (isAcknowledged is not null) q = q.Where(a => a.IsAcknowledged == isAcknowledged);
        if (isRecovered is not null)
            q = isRecovered.Value
                ? q.Where(a => a.RecoveryTime != null)
                : q.Where(a => a.RecoveryTime == null);

        var alarms = await q.OrderByDescending(a => a.AlarmTime).Take(Math.Min(Math.Max(take, 1), 5000)).ToListAsync();

        var pointIds = alarms.Select(a => a.PointId).Distinct().ToList();
        var units = await _db.Points.AsNoTracking()
            .Where(p => pointIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Unit);

        return Ok(ApiResult.Ok(alarms.Select(a => AlarmDto.From(a, units.GetValueOrDefault(a.PointId) ?? ""))));
    }

    /// <summary>确认报警</summary>
    [HttpPost("{id:int}/ack")]
    public async Task<IActionResult> Ack(int id)
    {
        var alarm = await _db.Alarms.FindAsync(id);
        if (alarm is null) return NotFound(ApiResult.Fail("报警记录不存在"));

        alarm.IsAcknowledged = true;
        alarm.AckTime = DateTime.Now;
        alarm.AckBy = "操作员";
        await _db.SaveChangesAsync();

        _logger.LogInformation("报警确认：{id}（{point} {type}）", alarm.Id, alarm.PointName, alarm.AlarmType);

        var unit = await _db.Points.AsNoTracking()
            .Where(p => p.Id == alarm.PointId)
            .Select(p => p.Unit)
            .FirstOrDefaultAsync();
        await _hub.Clients.All.SendAsync("AlarmUpdated", AlarmDto.From(alarm, unit ?? ""));
        return Ok(ApiResult.Ok(AlarmDto.From(alarm, unit ?? "")));
    }

    /// <summary>取消报警确认</summary>
    [HttpPost("{id:int}/unack")]
    public async Task<IActionResult> Unack(int id)
    {
        var alarm = await _db.Alarms.FindAsync(id);
        if (alarm is null) return NotFound(ApiResult.Fail("报警记录不存在"));

        alarm.IsAcknowledged = false;
        alarm.AckTime = null;
        alarm.AckBy = null;
        await _db.SaveChangesAsync();

        _logger.LogInformation("取消报警确认：{id}（{point} {type}）", alarm.Id, alarm.PointName, alarm.AlarmType);

        var unit = await _db.Points.AsNoTracking()
            .Where(p => p.Id == alarm.PointId)
            .Select(p => p.Unit)
            .FirstOrDefaultAsync();
        await _hub.Clients.All.SendAsync("AlarmUpdated", AlarmDto.From(alarm, unit ?? ""));
        return Ok(ApiResult.Ok(AlarmDto.From(alarm, unit ?? "")));
    }
}

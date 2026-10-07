using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Api;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;
using RollerMillMonitor.Models;
using RollerMillMonitor.PlcDrivers;
using RollerMillMonitor.Services;
using RollerMillMonitor.Validation;

namespace RollerMillMonitor.Controllers;

[ApiController]
[Route("api/points")]
public class PointsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RealTimeCache _cache;
    private readonly PlcDriverFactory _driverFactory;
    private readonly AlarmEngine _alarmEngine;
    private readonly TrendService _trendService;
    private readonly ILogger<PointsController> _logger;

    public PointsController(
        AppDbContext db,
        RealTimeCache cache,
        PlcDriverFactory driverFactory,
        AlarmEngine alarmEngine,
        TrendService trendService,
        ILogger<PointsController> logger)
    {
        _db = db;
        _cache = cache;
        _driverFactory = driverFactory;
        _alarmEngine = alarmEngine;
        _trendService = trendService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? deviceId, [FromQuery] int? plcId, [FromQuery] string? keyword)
    {
        var q = _db.Points.AsNoTracking()
            .Include(p => p.Device)
            .Include(p => p.Plc)
            .AsQueryable();

        if (deviceId is not null) q = q.Where(p => p.DeviceId == deviceId);
        if (plcId is not null) q = q.Where(p => p.PlcId == plcId);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(p => p.Name.Contains(k) || p.Code.Contains(k) || p.Address.Contains(k));
        }

        var points = await q.OrderBy(p => p.DeviceId).ThenBy(p => p.SortOrder).ToListAsync();

        var list = points.Select(p =>
        {
            var rt = _cache.Get(p.Id);
            var (status, reason) = PointStatusResolver.ResolveFull(p, p.Plc, p.Device.Enabled, rt);
            return new
            {
                p.Id,
                DeviceId = p.DeviceId,
                DeviceName = p.Device?.Name ?? "",
                PlcId = p.PlcId,
                PlcName = p.Plc?.Name ?? "",
                PlcProtocol = p.Plc?.Protocol ?? "",
                p.Name,
                p.Code,
                p.Address,
                p.DataType,
                p.Unit,
                p.Scale,
                p.Offset,
                p.Enabled,
                p.AlarmEnabled,
                p.HighHigh,
                p.High,
                p.Low,
                p.LowLow,
                p.AlarmDelayMs,
                p.RecoveryDelayMs,
                p.Deadband,
                p.SortOrder,
                p.UpdatedAt,
                Value = rt?.Value,
                Status = status,
                StatusName = PointStatuses.DisplayName(status),
                StatusReason = reason,
                Timestamp = rt?.Timestamp,
                IsSimulated = rt?.IsSimulated == true
            };
        });

        return Ok(ApiResult.Ok(list));
    }

    [HttpPost]
    public async Task<IActionResult> Create(PointDto dto)
    {
        var err = await PointValidator.ValidateAsync(_db, dto);
        if (!err.Success) return BadRequest(err);

        var point = new Point
        {
            DeviceId = dto.DeviceId!.Value,
            PlcId = dto.PlcId,
            Name = dto.Name.Trim(),
            Code = dto.Code?.Trim() ?? "",
            Address = dto.Address.Trim(),
            DataType = dto.DataType,
            Unit = dto.Unit,
            Scale = dto.Scale,
            Offset = dto.Offset,
            Enabled = dto.Enabled,
            AlarmEnabled = dto.AlarmEnabled,
            HighHigh = dto.HighHigh,
            High = dto.High,
            Low = dto.Low,
            LowLow = dto.LowLow,
            AlarmDelayMs = dto.AlarmDelayMs,
            RecoveryDelayMs = dto.RecoveryDelayMs,
            Deadband = dto.Deadband,
            SortOrder = dto.SortOrder
        };
        _db.Points.Add(point);
        await _db.SaveChangesAsync();

        _logger.LogInformation("新增测点：{point}（设备 {deviceId}，地址 {address}，类型 {dataType}）",
            point.Name, point.DeviceId, point.Address, point.DataType);
        return Ok(ApiResult.Ok(new { point.Id }));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PointDto dto)
    {
        var point = await _db.Points.FindAsync(id);
        if (point is null) return NotFound(ApiResult.Fail("测点不存在"));

        var err = await PointValidator.ValidateAsync(_db, dto, id);
        if (!err.Success) return BadRequest(err);

        point.DeviceId = dto.DeviceId!.Value;
        point.PlcId = dto.PlcId;
        point.Name = dto.Name.Trim();
        point.Code = dto.Code?.Trim() ?? "";
        point.Address = dto.Address.Trim();
        point.DataType = dto.DataType;
        point.Unit = dto.Unit;
        point.Scale = dto.Scale;
        point.Offset = dto.Offset;
        point.Enabled = dto.Enabled;
        point.AlarmEnabled = dto.AlarmEnabled;
        point.HighHigh = dto.HighHigh;
        point.High = dto.High;
        point.Low = dto.Low;
        point.LowLow = dto.LowLow;
        point.AlarmDelayMs = dto.AlarmDelayMs;
        point.RecoveryDelayMs = dto.RecoveryDelayMs;
        point.Deadband = dto.Deadband;
        point.SortOrder = dto.SortOrder;
        point.UpdatedAt = DateTime.Now;

        // 配置变更后清掉旧缓存状态，等待下一次采集
        _cache.GetOrCreate(id).Status = "Waiting";
        _cache.GetOrCreate(id).StatusReason = "配置已变更，等待采集";

        await _db.SaveChangesAsync();
        _logger.LogInformation("编辑测点：{id}（{name}，地址 {address}）", point.Id, point.Name, point.Address);
        return Ok(ApiResult.Ok());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var point = await _db.Points.FindAsync(id);
        if (point is null) return NotFound(ApiResult.Fail("测点不存在"));

        // 级联删除：相关报警与历史趋势数据一并处理
        _db.Points.Remove(point);
        await _db.SaveChangesAsync();
        _cache.GetOrCreate(id).Status = PointStatuses.Disabled;
        _cache.GetOrCreate(id).StatusReason = "测点已删除";

        _logger.LogInformation("删除测点：{id}（{name}），相关报警与历史数据已级联删除", point.Id, point.Name);
        return Ok(ApiResult.Ok());
    }

    /// <summary>立即读取：单次读取测点并更新缓存/趋势/报警</summary>
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> ReadNow(int id)
    {
        var point = await _db.Points
            .Include(p => p.Device)
            .Include(p => p.Plc)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (point is null) return NotFound(ApiResult.Fail("测点不存在"));

        if (point.Plc is null)
            return BadRequest(ApiResult.Fail("测点未绑定 PLC，无法读取（未配置）", "plcId"));
        if (!point.Plc.Enabled)
            return BadRequest(ApiResult.Fail($"PLC 已禁用：{point.Plc.Name}", "plcId"));

        var (cfgStatus, cfgReason) = PointStatusResolver.ResolveConfig(point, point.Plc);
        if (cfgStatus != PointStatuses.Normal)
            return BadRequest(ApiResult.Fail(cfgReason, "address"));

        IPlcDriver driver;
        try
        {
            driver = _driverFactory.GetOrCreate(point.Plc);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ApiResult.Fail(ex.Message));
        }

        if (!driver.IsConnected)
        {
            var connect = await driver.ConnectAsync(HttpContext.RequestAborted);
            if (!connect.Success)
            {
                var rtConn = _cache.GetOrCreate(id);
                rtConn.Status = PointStatuses.CommFault;
                rtConn.StatusReason = $"PLC 连接失败：{connect.Error}";
                rtConn.Timestamp = DateTime.Now;
                rtConn.ErrorMessage = connect.Error;
                return Ok(ApiResult.Ok(new
                {
                    success = false,
                    status = rtConn.Status,
                    statusName = PointStatuses.DisplayName(rtConn.Status),
                    message = rtConn.StatusReason,
                    value = (double?)null
                }));
            }
            point.Plc.ConnectionStatus = "Connected";
            point.Plc.LastConnectedAt = DateTime.Now;
            point.Plc.LastError = null;
        }

        var result = await driver.ReadAsync(point.Address, point.DataType, HttpContext.RequestAborted);
        if (!result.Success)
        {
            var rtRead = _cache.GetOrCreate(id);
            rtRead.Status = PointStatuses.ReadFailed;
            rtRead.StatusReason = $"读取失败：{result.Error}";
            rtRead.Timestamp = DateTime.Now;
            rtRead.ErrorMessage = result.Error;
            await _alarmEngine.EvaluatePointAsync(_db, point, PointStatuses.ReadFailed, null, HttpContext.RequestAborted);
            await _db.SaveChangesAsync();
            return Ok(ApiResult.Ok(new
            {
                success = false,
                status = rtRead.Status,
                statusName = PointStatuses.DisplayName(rtRead.Status),
                message = rtRead.StatusReason,
                value = (double?)null
            }));
        }

        var value = result.Value!.Value * point.Scale + point.Offset;
        var rt = _cache.GetOrCreate(id);
        rt.Status = PointStatuses.Normal;
        rt.StatusReason = "正常";
        rt.Value = Math.Round(value, 4);
        rt.Timestamp = DateTime.Now;
        rt.ErrorMessage = null;
        rt.IsSimulated = point.Plc.IsSimulated;

        _db.Trends.Add(new TrendRecord
        {
            PointId = point.Id,
            PointName = point.Name,
            DeviceId = point.DeviceId,
            DeviceName = point.Device?.Name ?? "",
            Value = value,
            Timestamp = DateTime.Now,
            Status = PointStatuses.Normal
        });

        await _alarmEngine.EvaluatePointAsync(_db, point, PointStatuses.Normal, value, HttpContext.RequestAborted);
        await _db.SaveChangesAsync();

        _logger.LogInformation("立即读取测点：{name} = {value}{unit}", point.Name, value, point.Unit);
        return Ok(ApiResult.Ok(new
        {
            success = true,
            status = rt.Status,
            statusName = PointStatuses.DisplayName(rt.Status),
            value,
            unit = point.Unit,
            timestamp = rt.Timestamp,
            isSimulated = rt.IsSimulated
        }));
    }
}

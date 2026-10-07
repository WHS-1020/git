using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Api;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;
using RollerMillMonitor.Models;
using RollerMillMonitor.Services;

namespace RollerMillMonitor.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RealTimeCache _cache;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(AppDbContext db, RealTimeCache cache, ILogger<DevicesController> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var devices = await _db.Devices.AsNoTracking()
            .Include(d => d.Points).ThenInclude(p => p.Plc)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Id)
            .ToListAsync();

        var list = devices.Select(d =>
        {
            var (status, name) = AggregateStatus(d);
            return new
            {
                d.Id,
                d.Name,
                d.Code,
                d.Description,
                d.Enabled,
                d.SortOrder,
                d.CreatedAt,
                PointCount = d.Points.Count,
                Status = status,
                StatusName = name
            };
        });

        return Ok(ApiResult.Ok(list));
    }

    [HttpPost]
    public async Task<IActionResult> Create(DeviceDto dto)
    {
        var err = await ValidateDeviceAsync(dto, null);
        if (err is not null) return BadRequest(err);

        var device = new Device
        {
            Name = dto.Name.Trim(),
            Code = dto.Code.Trim(),
            Description = dto.Description,
            Enabled = dto.Enabled,
            SortOrder = dto.SortOrder
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        _logger.LogInformation("新增设备：{name}（{code}）", device.Name, device.Code);
        return Ok(ApiResult.Ok(new { device.Id }));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DeviceDto dto)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null) return NotFound(ApiResult.Fail("设备不存在"));

        var err = await ValidateDeviceAsync(dto, id);
        if (err is not null) return BadRequest(err);

        device.Name = dto.Name.Trim();
        device.Code = dto.Code.Trim();
        device.Description = dto.Description;
        device.Enabled = dto.Enabled;
        device.SortOrder = dto.SortOrder;
        await _db.SaveChangesAsync();

        _logger.LogInformation("编辑设备：{id}（{name}）", device.Id, device.Name);
        return Ok(ApiResult.Ok());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device is null) return NotFound(ApiResult.Fail("设备不存在"));

        // 级联删除：该设备下测点、相关报警与历史趋势一并删除
        _db.Devices.Remove(device);
        await _db.SaveChangesAsync();

        _logger.LogInformation("删除设备：{id}（{name}），其下测点/报警/历史数据已级联删除", device.Id, device.Name);
        return Ok(ApiResult.Ok());
    }

    /// <summary>
    /// 复制设备配置：复制测点名称/单位/数据类型/报警阈值/排序等，
    /// 但不复制 PLC 地址与 PLC 绑定 —— 新设备 PLC 地址必须由用户确认后填写。
    /// </summary>
    [HttpPost("{id:int}/copy")]
    public async Task<IActionResult> Copy(int id)
    {
        var src = await _db.Devices.Include(d => d.Points)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (src is null) return NotFound(ApiResult.Fail("设备不存在"));

        var baseName = $"{src.Name}-副本";
        var baseCode = string.IsNullOrWhiteSpace(src.Code) ? "" : $"{src.Code}-C";
        var newName = baseName;
        var newCode = baseCode;
        var n = 1;
        while (await _db.Devices.AnyAsync(d => d.Name == newName))
            newName = $"{baseName}{n++}";
        while (!string.IsNullOrEmpty(newCode) && await _db.Devices.AnyAsync(d => d.Code == newCode))
            newCode = $"{baseCode}{n++}";

        var copy = new Device
        {
            Name = newName,
            Code = newCode,
            Description = src.Description is null ? "（复制设备）" : $"{src.Description}（复制）",
            Enabled = src.Enabled,
            SortOrder = src.SortOrder + 1
        };
        _db.Devices.Add(copy);

        // 先保存设备获得 Id，再复制测点（否则外键约束失败）
        await _db.SaveChangesAsync();

        foreach (var p in src.Points.OrderBy(p => p.SortOrder))
        {
            _db.Points.Add(new Point
            {
                DeviceId = copy.Id, // 先赋值，SaveChanges 后由外键关联
                Name = p.Name,
                Code = p.Code,
                PlcId = null,          // 不复制 PLC 绑定
                Address = string.Empty, // 不复制 PLC 地址（由用户确认）
                DataType = p.DataType,
                Unit = p.Unit,
                Scale = p.Scale,
                Offset = p.Offset,
                Enabled = p.Enabled,
                AlarmEnabled = p.AlarmEnabled,
                HighHigh = p.HighHigh,
                High = p.High,
                Low = p.Low,
                LowLow = p.LowLow,
                AlarmDelayMs = p.AlarmDelayMs,
                RecoveryDelayMs = p.RecoveryDelayMs,
                Deadband = p.Deadband,
                SortOrder = p.SortOrder
            });
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("复制设备配置：{src} → {copy}（测点已复制，PLC 地址留空待确认）",
            src.Name, copy.Name);
        return Ok(ApiResult.Ok(new { copy.Id, copy.Name, PointCount = src.Points.Count }));
    }

    private async Task<ApiResult?> ValidateDeviceAsync(DeviceDto dto, int? currentId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return ApiResult.Fail("请输入设备名称", "name");

        var duplicate = await _db.Devices.AnyAsync(d => d.Name == dto.Name.Trim() && d.Id != currentId);
        if (duplicate)
            return ApiResult.Fail($"设备名称已存在：{dto.Name.Trim()}", "name");

        if (!string.IsNullOrWhiteSpace(dto.Code))
        {
            var dupCode = await _db.Devices.AnyAsync(d => d.Code == dto.Code.Trim() && d.Id != currentId);
            if (dupCode)
                return ApiResult.Fail($"设备编码已存在：{dto.Code.Trim()}", "code");
        }

        return null;
    }

    /// <summary>设备综合状态（按严重程度取最差）：通信故障 > 读取失败 > 配置错误 > 未配置 > 等待采集 > 已禁用 > 正常</summary>
    private (string Status, string Name) AggregateStatus(Device d)
    {
        if (!d.Enabled)
            return (PointStatuses.Disabled, PointStatuses.DisplayName(PointStatuses.Disabled));

        var worst = 8;
        var worstStatus = PointStatuses.Normal;
        var worstName = PointStatuses.DisplayName(PointStatuses.Normal);

        foreach (var p in d.Points)
        {
            var rt = _cache.Get(p.Id);
            var (status, _) = PointStatusResolver.ResolveFull(p, p.Plc, d.Enabled, rt);
            var rank = status switch
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
            if (rank < worst)
            {
                worst = rank;
                worstStatus = status;
                worstName = PointStatuses.DisplayName(status);
            }
        }

        return (worstStatus, worstName);
    }
}

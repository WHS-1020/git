using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Api;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;
using RollerMillMonitor.Models;
using RollerMillMonitor.PlcDrivers;

namespace RollerMillMonitor.Controllers;

[ApiController]
[Route("api/plcs")]
public class PlcsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PlcDriverFactory _driverFactory;
    private readonly ILogger<PlcsController> _logger;

    public PlcsController(AppDbContext db, PlcDriverFactory driverFactory, ILogger<PlcsController> logger)
    {
        _db = db;
        _driverFactory = driverFactory;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var plcs = await _db.Plcs.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
        return Ok(ApiResult.Ok(plcs));
    }

    [HttpPost]
    public async Task<IActionResult> Create(PlcDto dto)
    {
        var err = await ValidatePlcAsync(dto, null);
        if (err is not null) return BadRequest(err);

        var plc = new Plc
        {
            Name = dto.Name.Trim(),
            Protocol = dto.Protocol,
            IpAddress = dto.IpAddress.Trim(),
            Port = dto.Port,
            Rack = dto.Rack,
            Slot = dto.Slot,
            Enabled = dto.Enabled,
            ConnectionStatus = "Unknown"
        };
        _db.Plcs.Add(plc);
        await _db.SaveChangesAsync();

        _logger.LogInformation("新增 PLC：{name}（{protocol} {ip}:{port}）", plc.Name, plc.Protocol, plc.IpAddress, plc.Port);
        return Ok(ApiResult.Ok(new { plc.Id }));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, PlcDto dto)
    {
        var plc = await _db.Plcs.FindAsync(id);
        if (plc is null) return NotFound(ApiResult.Fail("PLC 不存在"));

        var err = await ValidatePlcAsync(dto, id);
        if (err is not null) return BadRequest(err);

        plc.Name = dto.Name.Trim();
        plc.Protocol = dto.Protocol;
        plc.IpAddress = dto.IpAddress.Trim();
        plc.Port = dto.Port;
        plc.Rack = dto.Rack;
        plc.Slot = dto.Slot;
        plc.Enabled = dto.Enabled;

        // 配置变更：重建驱动连接（下次采集按新配置连接）
        _driverFactory.Invalidate(plc);
        plc.ConnectionStatus = "Unknown";
        plc.LastError = null;

        await _db.SaveChangesAsync();
        _logger.LogInformation("编辑 PLC：{id}（{name}）", plc.Id, plc.Name);
        return Ok(ApiResult.Ok());
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var plc = await _db.Plcs.FindAsync(id);
        if (plc is null) return NotFound(ApiResult.Fail("PLC 不存在"));

        // 测点 PlcId 置空（FK SetNull），测点保留但显示"未配置"
        _db.Plcs.Remove(plc);
        await _db.SaveChangesAsync();

        _driverFactory.Invalidate(plc);
        _logger.LogInformation("删除 PLC：{id}（{name}），其下测点已解除绑定（显示未配置）", plc.Id, plc.Name);
        return Ok(ApiResult.Ok());
    }

    /// <summary>测试 PLC 连接（连接成功后立即断开由驱动缓存管理）</summary>
    [HttpPost("{id:int}/test")]
    public async Task<IActionResult> Test(int id)
    {
        var plc = await _db.Plcs.FindAsync(id);
        if (plc is null) return NotFound(ApiResult.Fail("PLC 不存在"));

        IPlcDriver driver;
        try
        {
            driver = _driverFactory.GetOrCreate(plc);
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ApiResult.Fail(ex.Message));
        }

        var sw = Stopwatch.StartNew();
        PlcReadResult result;
        try
        {
            result = driver.IsConnected
                ? PlcReadResult.Ok(0)
                : await driver.ConnectAsync(HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            result = PlcReadResult.Fail($"测试连接异常：{ex.Message}");
        }
        sw.Stop();

        if (result.Success)
        {
            plc.ConnectionStatus = "Connected";
            plc.LastConnectedAt = DateTime.Now;
            plc.LastError = null;
            _logger.LogInformation("PLC 连接测试成功：{name}（{latency}ms）", plc.Name, sw.ElapsedMilliseconds);
        }
        else
        {
            plc.ConnectionStatus = "Fault";
            plc.LastError = result.Error;
            _logger.LogWarning("PLC 连接测试失败：{name}（{err}）", plc.Name, result.Error);
        }

        await _db.SaveChangesAsync();

        return Ok(ApiResult.Ok(new
        {
            success = result.Success,
            latencyMs = sw.ElapsedMilliseconds,
            message = result.Success ? "连接成功" : result.Error,
            connectionStatus = plc.ConnectionStatus,
            plc.LastConnectedAt,
            plc.LastError
        }));
    }

    private async Task<ApiResult?> ValidatePlcAsync(PlcDto dto, int? currentId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return ApiResult.Fail("请输入 PLC 名称", "name");

        if (!new[] { "S7", "Simulator", "ModbusTcp", "ModbusRtu" }
                .Contains(dto.Protocol, StringComparer.OrdinalIgnoreCase))
            return ApiResult.Fail($"不支持的协议：{dto.Protocol}（支持 S7 / ModbusTcp / ModbusRtu / Simulator）", "protocol");

        if (!dto.Protocol.Equals("Simulator", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(dto.IpAddress))
            return ApiResult.Fail("请输入 IP 地址", "ipAddress");

        if (dto.Port is < 1 or > 65535)
            return ApiResult.Fail("端口范围必须为 1-65535", "port");

        var duplicate = await _db.Plcs.AnyAsync(p => p.Name == dto.Name.Trim() && p.Id != currentId);
        if (duplicate)
            return ApiResult.Fail($"PLC 名称已存在：{dto.Name.Trim()}", "name");

        return null;
    }
}

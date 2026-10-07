using System.Text;
using Microsoft.AspNetCore.Mvc;
using RollerMillMonitor.Api;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;
using RollerMillMonitor.Services;

namespace RollerMillMonitor.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ConfigService _configService;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(AppDbContext db, ConfigService configService, ILogger<ConfigController> logger)
    {
        _db = db;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>导出配置。format=json 返回 JSON；format=csv 返回分节 CSV 文件。</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string format = "json")
    {
        var model = await _configService.BuildExportAsync(_db, HttpContext.RequestAborted);

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = _configService.ToCsv(model);
            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv; charset=utf-8", $"监控平台配置_{DateTime.Now:yyyyMMddHHmmss}.csv");
        }

        return Ok(ApiResult.Ok(model));
    }

    /// <summary>导入配置。请求体：{ format: "json"|"csv", content: "..." }。失败时返回具体行号与原因。</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(ConfigImportRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content))
            return BadRequest(ApiResult.Fail("导入内容不能为空", "content"));

        if (!req.Format.Equals("json", StringComparison.OrdinalIgnoreCase) &&
            !req.Format.Equals("csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResult.Fail("format 仅支持 json 或 csv", "format"));

        var result = await _configService.ImportAsync(_db, req.Format, req.Content, HttpContext.RequestAborted);

        if (result.Success)
        {
            _logger.LogInformation("配置导入成功：PLC {plc}，设备 {dev}，测点 {point}，阈值 {thr}",
                result.PlcCount, result.DeviceCount, result.PointCount, result.ThresholdCount);
            return Ok(ApiResult.Ok(new
            {
                plcCount = result.PlcCount,
                deviceCount = result.DeviceCount,
                pointCount = result.PointCount,
                thresholdCount = result.ThresholdCount
            }));
        }

        return BadRequest(new ApiResult
        {
            Success = false,
            Message = $"导入失败，共 {result.Errors.Count} 处错误",
            Data = new { errors = result.Errors }
        });
    }
}

public class ConfigImportRequest
{
    public string Format { get; set; } = "json";
    public string Content { get; set; } = string.Empty;
}

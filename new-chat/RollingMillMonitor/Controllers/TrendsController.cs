using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Api;
using RollerMillMonitor.Data;
using RollerMillMonitor.Services;

namespace RollerMillMonitor.Controllers;

[ApiController]
[Route("api/trends")]
public class TrendsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TrendService _trendService;
    private readonly ILogger<TrendsController> _logger;

    public TrendsController(AppDbContext db, TrendService trendService, ILogger<TrendsController> logger)
    {
        _db = db;
        _trendService = trendService;
        _logger = logger;
    }

    /// <summary>
    /// 趋势查询。参数：pointIds=1,2,3；from/to 为 ISO 时间；maxRows 限制返回行数。
    /// 默认最近 1 小时。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] string? pointIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int maxRows = 5000)
    {
        var ids = ParsePointIds(pointIds);
        if (ids.Length == 0)
            return BadRequest(ApiResult.Fail("请至少选择一个测点（pointIds）", "pointIds"));

        var to2 = to ?? DateTime.Now;
        var from2 = from ?? to2.AddHours(-1);

        var rows = await _trendService.QueryAsync(
            _db, ids, from2, to2, Math.Min(Math.Max(maxRows, 100), 20000), HttpContext.RequestAborted);

        var points = await _db.Points.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p);

        var series = ids.Select(id =>
        {
            var p = points.GetValueOrDefault(id);
            return new
            {
                pointId = id,
                name = p?.Name ?? $"测点#{id}",
                unit = p?.Unit ?? "",
                data = rows.Where(r => r.PointId == id)
                    .Select(r => new { t = r.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss"), v = r.Value })
                    .ToList()
            };
        });

        return Ok(ApiResult.Ok(new
        {
            series,
            rows = rows.Select(r => new
            {
                r.PointId,
                PointName = r.PointName,
                DeviceName = r.DeviceName,
                r.Value,
                r.Timestamp,
                r.Status
            }),
            from = from2,
            to = to2
        }));
    }

    /// <summary>导出 CSV</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? pointIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int maxRows = 20000)
    {
        var ids = ParsePointIds(pointIds);
        if (ids.Length == 0)
            return BadRequest(ApiResult.Fail("请至少选择一个测点（pointIds）", "pointIds"));

        var to2 = to ?? DateTime.Now;
        var from2 = from ?? to2.AddHours(-1);

        var rows = await _trendService.QueryAsync(
            _db, ids, from2, to2, Math.Min(Math.Max(maxRows, 100), 50000), HttpContext.RequestAborted);

        var sb = new StringBuilder();
        sb.AppendLine("时间,设备,测点,数值,单位,状态");
        var points = await _db.Points.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p);

        foreach (var r in rows)
        {
            var unit = points.GetValueOrDefault(r.PointId)?.Unit ?? "";
            sb.AppendLine(
                $"{r.Timestamp:yyyy-MM-dd HH:mm:ss},{EscapeCsv(r.DeviceName)},{EscapeCsv(r.PointName)}," +
                $"{r.Value?.ToString("0.####") ?? ""},{EscapeCsv(unit)},{r.Status}");
        }

        _logger.LogInformation("导出趋势 CSV：测点 {ids}，{count} 行", string.Join(",", ids), rows.Count);

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", $"趋势数据_{DateTime.Now:yyyyMMddHHmmss}.csv");
    }

    private static int[] ParsePointIds(string? pointIds)
    {
        if (string.IsNullOrWhiteSpace(pointIds)) return Array.Empty<int>();
        return pointIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .Where(v => v > 0)
            .Distinct()
            .ToArray();
    }

    private static string EscapeCsv(string s) =>
        s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}

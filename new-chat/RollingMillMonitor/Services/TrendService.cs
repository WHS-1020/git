using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Data;
using RollerMillMonitor.Models;

namespace RollerMillMonitor.Services;

/// <summary>
/// 历史趋势数据服务：批量写入、按测点/时间查询、自动清理过期数据。
/// </summary>
public class TrendService
{
    private readonly ILogger<TrendService> _logger;

    public TrendService(ILogger<TrendService> logger)
    {
        _logger = logger;
    }

    /// <summary>批量写入一段采集周期的趋势记录</summary>
    public async Task SaveBatchAsync(AppDbContext db, IEnumerable<TrendRecord> records, CancellationToken ct)
    {
        var list = records as List<TrendRecord> ?? records.ToList();
        if (list.Count == 0) return;

        db.Trends.AddRange(list);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 查询趋势。maxRows 用于限制返回行数（按时间倒序取最近 N 行后正序返回）。
    /// </summary>
    public async Task<List<TrendRecord>> QueryAsync(
        AppDbContext db, int[] pointIds, DateTime from, DateTime to, int maxRows, CancellationToken ct)
    {
        var query = db.Trends
            .Where(t => pointIds.Contains(t.PointId) && t.Timestamp >= from && t.Timestamp <= to);

        var rows = await query
            .OrderByDescending(t => t.Timestamp)
            .Take(maxRows)
            .ToListAsync(ct);

        rows.Reverse();
        return rows;
    }

    /// <summary>清理早于指定时间的趋势数据，返回删除行数</summary>
    public async Task<int> CleanupAsync(AppDbContext db, DateTime olderThan, CancellationToken ct)
    {
        var rows = await db.Trends.Where(t => t.Timestamp < olderThan).ToListAsync(ct);
        if (rows.Count == 0) return 0;

        db.Trends.RemoveRange(rows);
        var removed = await db.SaveChangesAsync(ct);
        _logger.LogInformation("趋势数据清理：删除 {count} 条早于 {time} 的记录", removed, olderThan);
        return removed;
    }
}

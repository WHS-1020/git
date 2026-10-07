using System.Collections.Concurrent;
using RollerMillMonitor.Models;

namespace RollerMillMonitor.Services;

/// <summary>测点实时状态（内存缓存，不落库）</summary>
public class PointRuntime
{
    public int PointId { get; set; }

    /// <summary>工程值（已应用倍率/偏移）</summary>
    public double? Value { get; set; }

    /// <summary>状态：Normal / ReadFailed / CommFault / ConfigError / ...</summary>
    public string Status { get; set; } = PointStatuses.NotConfigured;

    /// <summary>状态原因（显示给用户的具体原因）</summary>
    public string StatusReason { get; set; } = string.Empty;

    /// <summary>最后读取时间</summary>
    public DateTime? Timestamp { get; set; }

    /// <summary>是否为模拟数据（明确标记）</summary>
    public bool IsSimulated { get; set; }

    /// <summary>最近一次错误信息</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 实时值缓存：后台采集服务写入，总览/测点页面读取，SignalR 推送。
/// </summary>
public class RealTimeCache
{
    private readonly ConcurrentDictionary<int, PointRuntime> _map = new();

    public PointRuntime GetOrCreate(int pointId)
        => _map.GetOrAdd(pointId, id => new PointRuntime { PointId = id });

    public PointRuntime? Get(int pointId)
        => _map.TryGetValue(pointId, out var rt) ? rt : null;

    public IReadOnlyDictionary<int, PointRuntime> Snapshot() => _map;

    public void Update(PointRuntime rt) => _map[rt.PointId] = rt;

    public void Clear() => _map.Clear();
}

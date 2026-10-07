namespace RollerMillMonitor.Models;

/// <summary>历史趋势数据（按采集周期追加，可配置保留天数自动清理）</summary>
public class TrendRecord
{
    public int Id { get; set; }

    public int PointId { get; set; }

    /// <summary>测点名称快照</summary>
    public string PointName { get; set; } = string.Empty;

    public int DeviceId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    /// <summary>工程值（已应用倍率与偏移）</summary>
    public double? Value { get; set; }

    /// <summary>采集时刻</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>采集时的测点状态：Normal / ReadFailed / CommFault / ConfigError ...</summary>
    public string Status { get; set; } = string.Empty;
}

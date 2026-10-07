namespace RollerMillMonitor.Models;

/// <summary>
/// 报警记录（持久化）。同一测点同一类型同一时刻最多一条活动报警。
/// </summary>
public class AlarmRecord
{
    public int Id { get; set; }

    /// <summary>触发报警的测点</summary>
    public int PointId { get; set; }

    public int? DeviceId { get; set; }

    public int? PlcId { get; set; }

    /// <summary>报警类型：HighHigh / High / Low / LowLow / Comm</summary>
    public string AlarmType { get; set; } = string.Empty;

    /// <summary>报警级别：Critical（高高/低低/通信）/ Warning（高/低）</summary>
    public string AlarmLevel { get; set; } = "Warning";

    /// <summary>测点名称快照</summary>
    public string PointName { get; set; } = string.Empty;

    /// <summary>设备名称快照</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>报警描述，例如：油流过低（低于 10 L/min）</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>报警发生时的数值</summary>
    public double? AlarmValue { get; set; }

    /// <summary>报警阈值</summary>
    public double? Threshold { get; set; }

    public DateTime AlarmTime { get; set; } = DateTime.Now;

    /// <summary>恢复时间（空 = 尚未恢复）</summary>
    public DateTime? RecoveryTime { get; set; }

    /// <summary>是否已确认</summary>
    public bool IsAcknowledged { get; set; }

    public DateTime? AckTime { get; set; }

    public string? AckBy { get; set; }

    /// <summary>是否仍处于活动状态</summary>
    public bool IsActive { get; set; } = true;

    public Point Point { get; set; } = null!;
}

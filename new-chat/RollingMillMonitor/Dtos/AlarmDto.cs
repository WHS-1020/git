using RollerMillMonitor.Models;

namespace RollerMillMonitor.Dtos;

/// <summary>报警记录（对外返回/推送）</summary>
public class AlarmDto
{
    public int Id { get; set; }
    public int PointId { get; set; }
    public int? DeviceId { get; set; }
    public int? PlcId { get; set; }
    public string AlarmType { get; set; } = string.Empty;
    public string AlarmTypeName { get; set; } = string.Empty;
    public string AlarmLevel { get; set; } = string.Empty;
    public string PointName { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double? AlarmValue { get; set; }
    public double? Threshold { get; set; }
    public DateTime AlarmTime { get; set; }
    public DateTime? RecoveryTime { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? AckTime { get; set; }
    public string? AckBy { get; set; }
    public bool IsActive { get; set; }
    public string Unit { get; set; } = string.Empty;

    /// <summary>持续时间（秒，按当前时间或恢复时间计算）</summary>
    public double? DurationSeconds { get; set; }

    public static AlarmDto From(AlarmRecord a, string unit = "")
    {
        var end = a.RecoveryTime ?? DateTime.Now;
        return new AlarmDto
        {
            Id = a.Id,
            PointId = a.PointId,
            DeviceId = a.DeviceId,
            PlcId = a.PlcId,
            AlarmType = a.AlarmType,
            AlarmTypeName = AlarmTypes.DisplayName(a.AlarmType),
            AlarmLevel = a.AlarmLevel,
            PointName = a.PointName,
            DeviceName = a.DeviceName,
            Message = a.Message,
            AlarmValue = a.AlarmValue,
            Threshold = a.Threshold,
            AlarmTime = a.AlarmTime,
            RecoveryTime = a.RecoveryTime,
            IsAcknowledged = a.IsAcknowledged,
            AckTime = a.AckTime,
            AckBy = a.AckBy,
            IsActive = a.IsActive,
            Unit = unit,
            DurationSeconds = Math.Max(0, (end - a.AlarmTime).TotalSeconds)
        };
    }
}

/// <summary>报警类型与级别</summary>
public static class AlarmTypes
{
    public const string HighHigh = "HighHigh";
    public const string High = "High";
    public const string Low = "Low";
    public const string LowLow = "LowLow";
    public const string Comm = "Comm";

    /// <summary>开关量状态报警：Bool 测点 0 = 故障（如油流中断、油压不足）</summary>
    public const string Fault = "Fault";

    public const string LevelCritical = "Critical";
    public const string LevelWarning = "Warning";

    public static string DisplayName(string type) => type switch
    {
        HighHigh => "高高报警",
        High => "高报警",
        Low => "低报警",
        LowLow => "低低报警",
        Comm => "通信报警",
        Fault => "故障报警",
        _ => type
    };

    public static string LevelOf(string type) =>
        type is HighHigh or LowLow or Comm or Fault ? LevelCritical : LevelWarning;

    public static string Message(AlarmRecord a) => a.Message;
}

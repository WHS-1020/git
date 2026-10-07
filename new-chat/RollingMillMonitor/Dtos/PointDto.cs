namespace RollerMillMonitor.Dtos;

/// <summary>测点新增/编辑请求</summary>
public class PointDto
{
    public int? DeviceId { get; set; }
    public int? PlcId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DataType { get; set; } = "Real";
    public string Unit { get; set; } = string.Empty;
    public double Scale { get; set; } = 1;
    public double Offset { get; set; } = 0;
    public bool Enabled { get; set; } = true;
    public bool AlarmEnabled { get; set; }
    public double? HighHigh { get; set; }
    public double? High { get; set; }
    public double? Low { get; set; }
    public double? LowLow { get; set; }
    public int AlarmDelayMs { get; set; }
    public int RecoveryDelayMs { get; set; }
    public double Deadband { get; set; }
    public int SortOrder { get; set; }
}

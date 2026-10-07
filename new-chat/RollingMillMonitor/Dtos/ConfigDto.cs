namespace RollerMillMonitor.Dtos;

/// <summary>PLC 导出/导入条目</summary>
public class PlcExport
{
    public string Name { get; set; } = string.Empty;
    public string Protocol { get; set; } = "S7";
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 102;
    public int? Rack { get; set; } = 0;
    public int? Slot { get; set; } = 1;
    public bool Enabled { get; set; } = true;
}

/// <summary>测点导出/导入条目</summary>
public class PointExport
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string PlcName { get; set; } = string.Empty;
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

/// <summary>设备导出/导入条目</summary>
public class DeviceExport
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
    public List<PointExport> Points { get; set; } = new();
}

/// <summary>报警阈值导出/导入条目（与测点配置独立，便于单独校验）</summary>
public class AlarmThresholdExport
{
    public string DeviceName { get; set; } = string.Empty;
    public string PointName { get; set; } = string.Empty;
    public bool AlarmEnabled { get; set; }
    public double? HighHigh { get; set; }
    public double? High { get; set; }
    public double? Low { get; set; }
    public double? LowLow { get; set; }
    public int AlarmDelayMs { get; set; }
    public int RecoveryDelayMs { get; set; }
    public double Deadband { get; set; }
}

/// <summary>完整配置导出/导入模型（JSON 与 CSV 共用）</summary>
public class ConfigExportModel
{
    public DateTime ExportedAt { get; set; } = DateTime.Now;
    public List<PlcExport> Plcs { get; set; } = new();
    public List<DeviceExport> Devices { get; set; } = new();
    public List<AlarmThresholdExport> AlarmThresholds { get; set; } = new();
}

/// <summary>导入错误（带行号）</summary>
public class ImportError
{
    public int Line { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>导入结果</summary>
public class ImportResult
{
    public bool Success { get; set; }
    public List<ImportError> Errors { get; set; } = new();
    public int PlcCount { get; set; }
    public int DeviceCount { get; set; }
    public int PointCount { get; set; }
    public int ThresholdCount { get; set; }
}

namespace RollerMillMonitor.Dtos;

public class OverviewCounts
{
    public int Devices { get; set; }
    public int Points { get; set; }
    public int Normal { get; set; }
    public int Waiting { get; set; }
    public int NotConfigured { get; set; }
    public int ConfigError { get; set; }
    public int CommFault { get; set; }
    public int ReadFailed { get; set; }
    public int Disabled { get; set; }
    public int ActiveAlarms { get; set; }
}

public class OverviewPoint
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int? PlcId { get; set; }
    public double? Value { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string StatusReason { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public bool IsSimulated { get; set; }
    /// <summary>当前活动报警级别（Critical / Warning / null）</summary>
    public string? ActiveAlarmLevel { get; set; }
}

public class OverviewDevice
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public int SortOrder { get; set; }
    public string CommStatus { get; set; } = string.Empty;
    public string CommStatusName { get; set; } = string.Empty;
    public DateTime? LastUpdate { get; set; }
    public List<OverviewPoint> Points { get; set; } = new();
}

public class OverviewPlc
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool Enabled { get; set; }
    public bool IsSimulated { get; set; }
    public string ConnectionStatus { get; set; } = string.Empty;
    public DateTime? LastConnectedAt { get; set; }
    public string? LastError { get; set; }
}

public class OverviewDto
{
    public OverviewCounts Counts { get; set; } = new();
    public List<OverviewDevice> Devices { get; set; } = new();
    public List<OverviewPlc> Plcs { get; set; } = new();
    public List<AlarmDto> ActiveAlarms { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

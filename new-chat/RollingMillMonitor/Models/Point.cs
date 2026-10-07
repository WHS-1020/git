namespace RollerMillMonitor.Models;

/// <summary>
/// 测点：绑定到某个设备 + 某个 PLC 的一个采集项。
/// 真实采集的测点必须填写：名称、所属设备、绑定 PLC、PLC 地址、数据类型。
/// </summary>
public class Point
{
    public int Id { get; set; }

    /// <summary>所属设备</summary>
    public int DeviceId { get; set; }

    /// <summary>绑定的 PLC（可空：未配置）</summary>
    public int? PlcId { get; set; }

    /// <summary>测点名称，例如：温度、电流、油流、油压</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>测点编码，例如 TEMP-01</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>PLC 地址，例如 DB1.DBD0；模拟采集器用 SIM-001 等</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>数据类型：Bool / Int / DInt / UInt / Real / Float / Word / DWord</summary>
    public string DataType { get; set; } = "Real";

    /// <summary>工程单位，例如 °C、A、L/min、MPa</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>倍率：最终值 = 原始值 × Scale + Offset</summary>
    public double Scale { get; set; } = 1;

    /// <summary>偏移量</summary>
    public double Offset { get; set; } = 0;

    public bool Enabled { get; set; } = true;

    /// <summary>是否启用报警</summary>
    public bool AlarmEnabled { get; set; }

    /// <summary>高高报警阈值</summary>
    public double? HighHigh { get; set; }

    /// <summary>高报警阈值</summary>
    public double? High { get; set; }

    /// <summary>低报警阈值</summary>
    public double? Low { get; set; }

    /// <summary>低低报警阈值</summary>
    public double? LowLow { get; set; }

    /// <summary>报警延时（毫秒），避免短暂波动频繁报警</summary>
    public int AlarmDelayMs { get; set; } = 0;

    /// <summary>恢复延时（毫秒）</summary>
    public int RecoveryDelayMs { get; set; } = 0;

    /// <summary>死区：报警恢复阈值 = 报警阈值 ± 死区，避免临界抖动</summary>
    public double Deadband { get; set; } = 0;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Device Device { get; set; } = null!;

    public Plc? Plc { get; set; }
}

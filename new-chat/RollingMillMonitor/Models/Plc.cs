namespace RollerMillMonitor.Models;

/// <summary>
/// PLC 设备配置。
/// 协议目前支持：S7（西门子）、ModbusTcp、ModbusRtu、Simulator（模拟采集器，明确标记为模拟数据）。
/// </summary>
public class Plc
{
    public int Id { get; set; }

    /// <summary>PLC 名称，例如：主PLC</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>通信协议：S7 / ModbusTcp / ModbusRtu / Simulator</summary>
    public string Protocol { get; set; } = "S7";

    /// <summary>IP 地址，模拟采集器可填 127.0.0.1</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>端口，S7 默认 102</summary>
    public int Port { get; set; } = 102;

    /// <summary>机架号（S7 用）</summary>
    public int? Rack { get; set; } = 0;

    /// <summary>槽号（S7 用）</summary>
    public int? Slot { get; set; } = 1;

    /// <summary>是否启用</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>连接状态：Unknown / Connected / Disconnected / Fault</summary>
    public string ConnectionStatus { get; set; } = "Unknown";

    /// <summary>最后一次成功连接时间</summary>
    public DateTime? LastConnectedAt { get; set; }

    /// <summary>最后一次错误信息</summary>
    public string? LastError { get; set; }

    /// <summary>是否模拟采集器（明确标记，不伪装成真实 PLC）</summary>
    public bool IsSimulated => Protocol == "Simulator";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

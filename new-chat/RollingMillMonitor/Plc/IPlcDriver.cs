namespace RollerMillMonitor.PlcDrivers;

/// <summary>单次读取结果</summary>
public class PlcReadResult
{
    public bool Success { get; set; }
    public double? Value { get; set; }
    public string? Error { get; set; }

    public static PlcReadResult Ok(double value) => new() { Success = true, Value = value };
    public static PlcReadResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// PLC 驱动抽象。实际协议必须明确：
///   S7        —— 西门子 S7（S7netplus 实现）
///   Simulator —— 模拟采集器（未提供真实 PLC 协议/地址表时使用，明确标记为模拟数据）
///   ModbusTcp / ModbusRtu —— 预留协议，驱动未实现时返回明确错误
/// </summary>
public interface IPlcDriver : IDisposable
{
    bool IsConnected { get; }

    /// <summary>建立连接。返回是否成功及错误信息。</summary>
    Task<PlcReadResult> ConnectAsync(CancellationToken ct);

    void Disconnect();

    /// <summary>按地址读取并解析为工程数值（未应用倍率/偏移）。</summary>
    Task<PlcReadResult> ReadAsync(string address, string dataType, CancellationToken ct);
}

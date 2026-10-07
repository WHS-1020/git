using System.Collections.Concurrent;
using RollerMillMonitor.Models;

namespace RollerMillMonitor.PlcDrivers;

/// <summary>
/// PLC 驱动工厂：根据协议创建驱动实例，并按配置缓存复用。
/// 协议未实现时抛出明确异常（如 ModbusTcp / ModbusRtu）。
/// </summary>
public class PlcDriverFactory
{
    // 键：plcId + 配置哈希。配置变更（IP/端口/机架/槽号/协议）后自动重建驱动。
    private readonly ConcurrentDictionary<string, IPlcDriver> _drivers = new();

    public string ConfigKey(Plc plc) =>
        $"{plc.Id}|{plc.Protocol}|{plc.IpAddress}|{plc.Port}|{plc.Rack}|{plc.Slot}";

    /// <summary>
    /// 获取（或创建）驱动。协议未实现时抛出 NotSupportedException。
    /// </summary>
    public IPlcDriver GetOrCreate(Plc plc)
    {
        var key = ConfigKey(plc);

        if (_drivers.TryGetValue(key, out var existing))
            return existing;

        IPlcDriver driver = plc.Protocol.ToUpperInvariant() switch
        {
            "S7" => new S7PlcDriver(plc),
            "SIMULATOR" => new SimulatedPlcDriver(),
            "MODBUSTCP" => throw new NotSupportedException("Modbus TCP 驱动尚未实现，请先选择 S7 或模拟采集器协议"),
            "MODBUSRTU" => throw new NotSupportedException("Modbus RTU 驱动尚未实现，请先选择 S7 或模拟采集器协议"),
            _ => throw new NotSupportedException($"不支持的 PLC 协议：{plc.Protocol}")
        };

        _drivers.TryAdd(key, driver);
        return driver;
    }

    /// <summary>配置变更后移除旧驱动（连接会被释放，下次访问重建）</summary>
    public void Invalidate(Plc plc)
    {
        var key = ConfigKey(plc);
        if (_drivers.TryRemove(key, out var driver))
            driver.Dispose();
    }

    public void InvalidateAll()
    {
        foreach (var kv in _drivers)
        {
            if (_drivers.TryRemove(kv.Key, out var driver))
                driver.Dispose();
        }
    }
}

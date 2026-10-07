using System.Globalization;

namespace RollerMillMonitor.PlcDrivers;

/// <summary>
/// 模拟采集器：在未提供真实 PLC 协议、地址表时使用。
/// 数据为程序生成的正弦波 + 噪声，仅在界面/数据上明确标记为【模拟数据】，绝不伪装成真实 PLC。
/// 常见地址模板（方便演示）：
///   SIM-001 温度 ~38.5°C   SIM-002 电流 ~42A
///   SIM-003 油流开关量（1=正常，周期性短暂 0=故障，用于演示故障报警）
///   SIM-004 油压开关量（1=正常，周期性短暂 0=故障，用于演示故障报警）
/// </summary>
public class SimulatedPlcDriver : IPlcDriver
{
    private static readonly DateTime StartTime = DateTime.Now;

    public bool IsConnected { get; private set; }

    public Task<PlcReadResult> ConnectAsync(CancellationToken ct)
    {
        IsConnected = true;
        return Task.FromResult(PlcReadResult.Ok(0));
    }

    public void Disconnect() => IsConnected = false;

    public Task<PlcReadResult> ReadAsync(string address, string dataType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(address))
            return Task.FromResult(PlcReadResult.Fail("模拟地址为空"));

        var seconds = (DateTime.Now - StartTime).TotalSeconds;
        var value = address.ToUpperInvariant() switch
        {
            "SIM-001" => Sine(38.5, 2.0, 90, 0.0, seconds),      // 温度 °C
            "SIM-002" => Sine(42.0, 3.5, 75, 1.2, seconds),      // 电流 A
            // 开关量状态测点：每 150 秒出现 8 秒"故障"（0），其余时间"正常"（1），便于观察故障报警产生/恢复
            "SIM-003" => SwitchState(150, 8, seconds),            // 油流：1=正常，0=无油流/油流故障
            "SIM-004" => SwitchState(150, 8, seconds),            // 油压：1=正常，0=压力不足/压力故障
            _ => Sine(HashBase(address), 10.0, 45, HashPhase(address), seconds)
        };

        return Task.FromResult(PlcReadResult.Ok(Math.Round(value, 3)));
    }

    /// <summary>开关量状态：每 cycleSec 秒中有 faultSec 秒为 0（故障），其余为 1（正常）</summary>
    private static double SwitchState(int cycleSec, int faultSec, double t)
    {
        var phase = t % cycleSec;
        return phase < faultSec ? 0 : 1;
    }

    private static double Sine(double baseValue, double amp, double periodSec, double phase, double t)
    {
        var noise = Math.Sin(t * 13.7 + phase * 3) * amp * 0.08;
        return baseValue + amp * Math.Sin(2 * Math.PI * t / periodSec + phase) + noise;
    }

    private static double HashBase(string address)
    {
        var h = 0;
        foreach (var c in address) h = (h * 31 + c) % 997;
        return h % 100;
    }

    private static double HashPhase(string address)
    {
        var h = 0;
        foreach (var c in address) h = (h * 17 + c) % 97;
        return h / 10.0;
    }

    public void Dispose() => Disconnect();
}

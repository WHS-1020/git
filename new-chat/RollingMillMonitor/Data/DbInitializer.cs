using RollerMillMonitor.Models;

namespace RollerMillMonitor.Data;

/// <summary>
/// 数据库初始化数据（仅首次启动、库为空时执行）：
///  PLC：主PLC（模拟采集器，明确标记为模拟数据，可替换为真实 S7 PLC）
///  设备：1#轧机、2#轧机
///  每台设备测点：温度（Real）、电流（Real）、油流（Bool 开关量状态）、油压（Bool 开关量状态）
/// 1#轧机绑定模拟采集器（SIM-001~004，页面明确显示"模拟数据"）；
/// 2#轧机测点 PLC 地址为空，页面明确标记"未配置：请填写 PLC 地址"。
/// 油流/油压现场结构：检测元件 → PLC 柜 I/O → PLC 只提供开关量状态（1=正常，0=故障），
/// WinCC 负责状态显示与报警；因此油流/油压为 Bool 状态测点，0 触发"故障报警"。
/// 真实现场必须由用户填写真实 PLC 地址，本程序不虚构任何真实地址。
/// </summary>
public static class DbInitializer
{
    public static void Seed(AppDbContext db)
    {
        if (db.Plcs.Any() || db.Devices.Any())
            return;

        // 主PLC：模拟采集器（未提供真实 PLC 协议/地址表前的可替换方案）
        var mainPlc = new Plc
        {
            Name = "主PLC",
            Protocol = "Simulator",
            IpAddress = "127.0.0.1",
            Port = 102,
            Rack = 0,
            Slot = 1,
            Enabled = true,
            ConnectionStatus = "Unknown"
        };
        db.Plcs.Add(mainPlc);

        var rolling1 = new Device
        {
            Name = "1#轧机",
            Code = "ROLLING-01",
            Description = "1号轧机（演示：绑定模拟采集器，数据为模拟数据）",
            Enabled = true,
            SortOrder = 1
        };
        var rolling2 = new Device
        {
            Name = "2#轧机",
            Code = "ROLLING-02",
            Description = "2号轧机（演示：未配置 PLC 地址状态）",
            Enabled = true,
            SortOrder = 2
        };
        db.Devices.AddRange(rolling1, rolling2);

        // 先保存以获得实体 Id（否则外键约束失败）
        db.SaveChanges();

        // 1#轧机：绑定模拟 PLC，地址 SIM-001~004（模拟数据，明确标记）
        // 温度/电流：Real 模拟量；油流/油压：Bool 开关量状态（1=正常，0=故障 → 故障报警）
        db.Points.AddRange(
            Point(rolling1, mainPlc, "温度", "TEMP-01", "SIM-001", "Real", "°C", 1, 0, 1, false),
            Point(rolling1, mainPlc, "电流", "CURR-01", "SIM-002", "Real", "A", 1, 0, 2, false),
            Point(rolling1, mainPlc, "油流", "OILF-01", "SIM-003", "Bool", "", 1, 0, 3, true,
                alarmDelayMs: 2000, recoveryDelayMs: 3000),
            Point(rolling1, mainPlc, "油压", "OILP-01", "SIM-004", "Bool", "", 1, 0, 4, true,
                alarmDelayMs: 2000, recoveryDelayMs: 3000)
        );

        // 2#轧机：不绑定 PLC、地址为空 → 页面显示"未配置：请填写 PLC 地址"
        db.Points.AddRange(
            Point(rolling2, null, "温度", "TEMP-02", "", "Real", "°C", 1, 0, 1),
            Point(rolling2, null, "电流", "CURR-02", "", "Real", "A", 1, 0, 2),
            Point(rolling2, null, "油流", "OILF-02", "", "Bool", "", 1, 0, 3),
            Point(rolling2, null, "油压", "OILP-02", "", "Bool", "", 1, 0, 4)
        );

        db.SaveChanges();
    }

    private static Point Point(
        Device device, Plc? plc, string name, string code, string address,
        string dataType, string unit, double scale, double offset, int sortOrder,
        bool alarmEnabled = false, double? highHigh = null, double? high = null,
        double? low = null, double? lowLow = null, int alarmDelayMs = 0,
        int recoveryDelayMs = 0, double deadband = 0)
    {
        return new Point
        {
            DeviceId = device.Id,
            PlcId = plc?.Id,
            Name = name,
            Code = code,
            Address = address,
            DataType = dataType,
            Unit = unit,
            Scale = scale,
            Offset = offset,
            Enabled = true,
            AlarmEnabled = alarmEnabled,
            HighHigh = highHigh,
            High = high,
            Low = low,
            LowLow = lowLow,
            AlarmDelayMs = alarmDelayMs,
            RecoveryDelayMs = recoveryDelayMs,
            Deadband = deadband,
            SortOrder = sortOrder
        };
    }
}

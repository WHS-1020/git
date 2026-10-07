using RollerMillMonitor.Models;
using RollerMillMonitor.Validation;

namespace RollerMillMonitor.Services;

/// <summary>
/// 测点状态判定：
///  设备/测点/PLC 禁用 → 已禁用
///  缺少 PLC 或地址   → 未配置
///  地址解析失败/类型不匹配 → 配置错误
///  PLC 无法连接      → 通信故障（采集服务写入）
///  PLC 已连接但读取失败 → 读取失败（采集服务写入）
///  成功读取数据       → 正常（采集服务写入）
/// 注意：没有实时值 ≠ 未配置；是否配置由 PLC/地址/数据类型决定。
/// </summary>
public static class PointStatusResolver
{
    /// <summary>
    /// 仅基于配置判定（不含通信/读取结果）。返回 (状态, 原因)。
    /// 返回 Normal 表示配置完整、等待采集；采集服务会进一步更新为通信状态。
    /// </summary>
    public static (string Status, string Reason) ResolveConfig(Point p, Plc? plc)
    {
        if (plc is null)
            return (PointStatuses.NotConfigured, "缺少绑定的 PLC");

        if (!plc.Enabled)
            return (PointStatuses.Disabled, "PLC 已禁用");

        if (string.IsNullOrWhiteSpace(p.Address))
            return (PointStatuses.NotConfigured, "缺少 PLC 地址");

        if (!DataTypeRegistry.IsSupported(p.DataType))
            return (PointStatuses.ConfigError, $"不支持的数据类型：{p.DataType}");

        var parsed = AddressParser.Parse(plc.Protocol, p.Address.Trim());
        if (parsed is null)
            return (PointStatuses.ConfigError, $"PLC 地址格式错误：{p.Address}");

        if (!AddressParser.TypeMatches(parsed, p.DataType))
            return (PointStatuses.ConfigError, $"地址 {p.Address} 与数据类型 {p.DataType} 不匹配");

        return (PointStatuses.Normal, "配置完整，等待采集");
    }

    /// <summary>
    /// 结合配置与实时缓存得到最终状态。
    /// </summary>
    public static (string Status, string Reason) ResolveFull(Point p, Plc? plc, bool deviceEnabled, PointRuntime? rt)
    {
        if (!deviceEnabled)
            return (PointStatuses.Disabled, "设备已禁用");

        if (!p.Enabled)
            return (PointStatuses.Disabled, "测点已禁用");

        var (configStatus, configReason) = ResolveConfig(p, plc);
        if (configStatus != PointStatuses.Normal)
            return (configStatus, configReason);

        if (rt is null)
            return ("Waiting", "已配置，等待首次采集");

        return (rt.Status, rt.StatusReason);
    }
}

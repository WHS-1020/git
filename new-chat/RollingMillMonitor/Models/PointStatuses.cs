namespace RollerMillMonitor.Models;

/// <summary>
/// 测点/设备状态枚举（不把"没有实时值"直接等同于"未配置"：
/// 是否配置由 PLC/地址/数据类型决定，通信是否正常由连接与读取结果决定）。
/// </summary>
public static class PointStatuses
{
    /// <summary>未配置：缺少 PLC 或地址或数据类型</summary>
    public const string NotConfigured = "NotConfigured";

    /// <summary>配置错误：地址解析失败、类型不匹配</summary>
    public const string ConfigError = "ConfigError";

    /// <summary>通信故障：PLC 无法连接</summary>
    public const string CommFault = "CommFault";

    /// <summary>读取失败：PLC 已连接但读取失败</summary>
    public const string ReadFailed = "ReadFailed";

    /// <summary>正常：成功读取到数据</summary>
    public const string Normal = "Normal";

    /// <summary>已禁用：设备/测点/PLC 被禁用</summary>
    public const string Disabled = "Disabled";

    /// <summary>状态中文名</summary>
    public static string DisplayName(string status) => status switch
    {
        NotConfigured => "未配置",
        ConfigError => "配置错误",
        CommFault => "通信故障",
        ReadFailed => "读取失败",
        Normal => "正常",
        Disabled => "已禁用",
        _ => status
    };
}

using System.Text.RegularExpressions;

namespace RollerMillMonitor.Validation;

/// <summary>地址解析结果</summary>
public class ParsedAddress
{
    /// <summary>Bool / Word / DWord / Any（模拟器）</summary>
    public string Kind { get; set; } = "Any";

    public string Protocol { get; set; } = "S7";

    /// <summary>DB 块号（DB 区）</summary>
    public int? DbNumber { get; set; }

    /// <summary>字节偏移</summary>
    public int ByteOffset { get; set; }

    /// <summary>位偏移（Bool 用）</summary>
    public int? Bit { get; set; }

    /// <summary>区域：DB / M / I / Q</summary>
    public string Area { get; set; } = "DB";
}

/// <summary>
/// PLC 地址解析器。S7 协议支持：
///   DB1.DBX0.0（Bool） DB1.DBW0（2字节） DB1.DBD0（4字节）
///   M0.0 / M10.0（Bool） MW10 / MD20（M区字/双字）
///   I0.0 / Q0.0（Bool）  IW10 / QW10 / ID20 / QD20
/// 模拟采集器（Simulator）接受任意非空地址，例如 SIM-001。
/// </summary>
public static class AddressParser
{
    // DB1.DBX0.0 / DB1.DBW0 / DB1.DBD0
    private static readonly Regex DbRegex = new(
        @"^DB(?<db>\d+)\.DB(?<w>[XWD])(?<byte>\d+)(\.(?<bit>\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // M0.0 / MW10 / MD20 / I0.0 / IW10 / ID20 / Q0.0 / QW10 / QD20
    private static readonly Regex AreaRegex = new(
        @"^(?<area>[MIQ])(?<w>[WD])?(?<byte>\d+)(\.(?<bit>\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsSimulatorAddress(string protocol) =>
        string.Equals(protocol, "Simulator", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 解析地址。返回 null 表示格式错误。
    /// </summary>
    public static ParsedAddress? Parse(string protocol, string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        // 模拟采集器：接受任意非空地址（明确标记为模拟数据）
        if (IsSimulatorAddress(protocol))
        {
            return new ParsedAddress { Protocol = "Simulator", Kind = "Any" };
        }

        var m = DbRegex.Match(address);
        if (m.Success)
        {
            var kind = m.Groups["w"].Value.ToUpperInvariant() switch
            {
                "X" => "Bool",
                "W" => "Word",
                "D" => "DWord",
                _ => "Word"
            };
            return new ParsedAddress
            {
                Protocol = "S7",
                Kind = kind,
                Area = "DB",
                DbNumber = int.Parse(m.Groups["db"].Value),
                ByteOffset = int.Parse(m.Groups["byte"].Value),
                Bit = m.Groups["bit"].Success ? int.Parse(m.Groups["bit"].Value) : null
            };
        }

        var a = AreaRegex.Match(address);
        if (a.Success)
        {
            var hasBit = a.Groups["bit"].Success;
            var area = a.Groups["area"].Value.ToUpperInvariant();
            var width = a.Groups["w"].Value.ToUpperInvariant();

            // 带位偏移必须是 Bool
            if (hasBit)
            {
                return new ParsedAddress
                {
                    Protocol = "S7", Kind = "Bool", Area = area,
                    ByteOffset = int.Parse(a.Groups["byte"].Value),
                    Bit = int.Parse(a.Groups["bit"].Value)
                };
            }

            var kind = width switch
            {
                "W" => "Word",
                "D" => "DWord",
                _ => "Word"
            };
            return new ParsedAddress
            {
                Protocol = "S7", Kind = kind, Area = area,
                ByteOffset = int.Parse(a.Groups["byte"].Value)
            };
        }

        return null;
    }

    /// <summary>
    /// 校验地址与数据类型是否匹配。
    /// </summary>
    public static bool TypeMatches(ParsedAddress addr, string dataType)
    {
        if (addr.Kind == "Any") return true;
        var width = DataTypeRegistry.ByteWidth(dataType);
        return addr.Kind == width;
    }
}

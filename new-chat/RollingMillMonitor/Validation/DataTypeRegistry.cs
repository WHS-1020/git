namespace RollerMillMonitor.Validation;

/// <summary>
/// 数据类型注册表：平台支持的数据类型。
/// 实际支持哪些类型由 PLC 协议实现决定（Bool/2字节/4字节）。
/// </summary>
public static class DataTypeRegistry
{
    /// <summary>平台支持的全部数据类型</summary>
    public static readonly string[] SupportedTypes =
        { "Bool", "Int", "DInt", "UInt", "Real", "Float", "Word", "DWord" };

    public static bool IsSupported(string dataType) =>
        SupportedTypes.Contains(dataType, StringComparer.OrdinalIgnoreCase);

    public static bool IsBool(string dataType) =>
        string.Equals(dataType, "Bool", StringComparison.OrdinalIgnoreCase);

    public static bool IsNumeric(string dataType) =>
        IsSupported(dataType) && !IsBool(dataType);

    /// <summary>地址宽度类别（按字节）：Bool=1位 / Word=2字节 / DWord=4字节</summary>
    public static string ByteWidth(string dataType) => dataType.ToUpperInvariant() switch
    {
        "BOOL" => "Bool",
        "INT" or "UINT" or "WORD" => "Word",
        "DINT" or "REAL" or "FLOAT" or "DWORD" => "DWord",
        _ => "Unknown"
    };
}

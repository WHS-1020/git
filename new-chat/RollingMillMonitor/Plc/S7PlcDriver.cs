using RollerMillMonitor.Models;
using RollerMillMonitor.Validation;

namespace RollerMillMonitor.PlcDrivers;

/// <summary>
/// 西门子 S7 驱动（S7netplus 实现）。
/// 支持地址：DB1.DBX0.0 / DB1.DBW0 / DB1.DBD0 / M0.0 / MW10 / MD20 / IW10 / ID20 / QW10 / QD20
/// 读取方式：ReadBytes 读原始字节 → 按测点配置的数据类型手动解析（S7 为大端，.NET 为本机小端，需反转字节）。
/// 说明：不要使用 S7netplus 的字符串 Read("DB58.DBD0") —— 它无法识别数据类型，
///       会把 DBD 默认按 DWord（无符号整数）解析，导致 Real 浮点被当成超大整数（如 1107663258）。
/// 采集服务中单个测点失败不会影响其他测点。
/// </summary>
public class S7PlcDriver : IPlcDriver
{
    private readonly S7.Net.Plc _plc;

    public S7PlcDriver(Plc config)
    {
        _plc = new S7.Net.Plc(
            S7.Net.CpuType.S71500,
            config.IpAddress,
            (short)(config.Rack ?? 0),
            (short)(config.Slot ?? 1));
        _plc.ReadTimeout = 3000;
        _plc.WriteTimeout = 3000;
    }

    public bool IsConnected => _plc.IsConnected;

    public async Task<PlcReadResult> ConnectAsync(CancellationToken ct)
    {
        try
        {
            // S7netplus 的 Open() 失败时抛异常；成功后通过 IsConnected 确认
            await Task.Run(() => _plc.Open(), ct);
            return _plc.IsConnected
                ? PlcReadResult.Ok(0)
                : PlcReadResult.Fail("PLC 连接失败（Open 后未处于连接状态）");
        }
        catch (OperationCanceledException)
        {
            return PlcReadResult.Fail("连接超时（已取消）");
        }
        catch (Exception ex)
        {
            return PlcReadResult.Fail($"PLC 连接失败：{ex.Message}");
        }
    }

    public void Disconnect()
    {
        try
        {
            if (_plc.IsConnected) _plc.Close();
        }
        catch
        {
            // 关闭失败无需抛出，交由下次连接处理
        }
    }

    public async Task<PlcReadResult> ReadAsync(string address, string dataType, CancellationToken ct)
    {
        try
        {
            if (!_plc.IsConnected)
                return PlcReadResult.Fail("PLC 未连接");

            var upper = address.Trim().ToUpperInvariant();

            // 1) 解析地址（复用平台地址解析器）
            var parsed = AddressParser.Parse("S7", address.Trim());
            if (parsed is null)
                return PlcReadResult.Fail($"PLC 地址格式错误：{upper}（S7 协议）");

            // 2) 区域 → S7netplus DataType
            var s7Type = parsed.Area.ToUpperInvariant() switch
            {
                "DB" => S7.Net.DataType.DataBlock,
                "M" => S7.Net.DataType.Memory,
                "I" => S7.Net.DataType.Input,
                "Q" => S7.Net.DataType.Output,
                _ => S7.Net.DataType.DataBlock
            };
            var db = parsed.DbNumber ?? 0;
            var startByte = parsed.ByteOffset;

            // 3) 按测点数据类型确定读取字节数（Bool=1字节取位，Word=2字节，DWord=4字节）
            var width = DataTypeRegistry.ByteWidth(dataType);
            var length = width switch
            {
                "Bool" => 1,
                "Word" => 2,
                "DWord" => 4,
                _ => 4
            };

            // 4) 读取原始字节（S7 返回大端字节序）
            var bytes = await _plc.ReadBytesAsync(s7Type, db, startByte, length, ct);
            if (bytes is null || bytes.Length < length)
                return PlcReadResult.Fail($"地址 {upper} 读取结果为空（期望 {length} 字节）");

            // 5) 按数据类型手动解析（大端 → 小端反转字节）
            double value = ParseByType(bytes, dataType, parsed.Bit);
            return PlcReadResult.Ok(value);
        }
        catch (OperationCanceledException)
        {
            return PlcReadResult.Fail("读取超时");
        }
        catch (Exception ex)
        {
            return PlcReadResult.Fail($"读取 {address} 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 按数据类型解析原始字节。S7 协议为大端存储，.NET BitConverter 是本机小端，
    /// 因此多字节类型必须先反转字节数组再转换 —— 否则 Real 会解析成十亿级错误数值。
    /// </summary>
    private static double ParseByType(byte[] bytes, string dataType, int? bit)
    {
        var type = dataType.ToUpperInvariant();
        var buf = (byte[])bytes.Clone();

        switch (type)
        {
            case "BOOL":
                // 位地址（DBX0.0 / M0.0 / I0.0 / Q0.0）：取指定位；字地址（DBB0）取最低位
                return ((buf[0] >> (bit ?? 0)) & 1) == 1 ? 1 : 0;

            case "INT":   // 16 位有符号
                Array.Reverse(buf);
                return BitConverter.ToInt16(buf, 0);

            case "UINT":  // 16 位无符号
            case "WORD":
                Array.Reverse(buf);
                return BitConverter.ToUInt16(buf, 0);

            case "DINT":  // 32 位有符号
                Array.Reverse(buf);
                return BitConverter.ToInt32(buf, 0);

            case "DWORD": // 32 位无符号
                Array.Reverse(buf);
                return BitConverter.ToUInt32(buf, 0);

            case "REAL":  // 32 位浮点数（重点：必须反转字节）
            case "FLOAT":
                Array.Reverse(buf);
                return BitConverter.ToSingle(buf, 0);

            default:
                throw new NotSupportedException($"S7 驱动不支持的数据类型：{dataType}");
        }
    }

    public void Dispose() => Disconnect();
}

using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Api;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;

namespace RollerMillMonitor.Validation;

/// <summary>
/// 测点保存校验：后端必须验证 PlcId、Address、DataType、地址格式、类型匹配等，
/// 不保存半配置数据，错误提示明确到具体字段。
/// </summary>
public static class PointValidator
{
    public static async Task<ApiResult> ValidateAsync(
        AppDbContext db, PointDto dto, int? currentPointId = null)
    {
        // 1. 所属设备
        if (dto.DeviceId is null or <= 0)
            return ApiResult.Fail("请选择所属设备", "deviceId");

        var device = await db.Devices.FindAsync(dto.DeviceId.Value);
        if (device is null)
            return ApiResult.Fail("所属设备不存在", "deviceId");

        // 2. 测点名称
        if (string.IsNullOrWhiteSpace(dto.Name))
            return ApiResult.Fail("请输入测点名称", "name");

        // 3. 绑定的 PLC
        if (dto.PlcId is null or <= 0)
            return ApiResult.Fail("请选择绑定的 PLC", "plcId");

        var plc = await db.Plcs.FindAsync(dto.PlcId.Value);
        if (plc is null)
            return ApiResult.Fail("绑定的 PLC 不存在", "plcId");

        // 4. 数据类型
        if (!DataTypeRegistry.IsSupported(dto.DataType))
            return ApiResult.Fail($"不支持的数据类型：{dto.DataType}", "dataType");

        // 5. PLC 地址
        if (string.IsNullOrWhiteSpace(dto.Address))
            return ApiResult.Fail("请输入 PLC 地址", "address");

        var parsed = AddressParser.Parse(plc.Protocol, dto.Address.Trim());
        if (parsed is null)
            return ApiResult.Fail($"PLC 地址格式错误：{dto.Address}（{plc.Protocol} 协议）", "address");

        if (!AddressParser.TypeMatches(parsed, dto.DataType))
            return ApiResult.Fail(
                $"地址 {dto.Address} 不支持 {dto.DataType} 类型（{plc.Protocol} 协议下该地址为 {WidthName(parsed.Kind)}）",
                "address");

        // 6. 报警配置合法性
        // Bool 开关量状态测点（油流/油压等）：启用报警不需要数值阈值，0=故障触发"故障报警"；
        // 数值测点：启用报警必须至少配置一个阈值，且高低阈值大小关系必须正确。
        var isBool = DataTypeRegistry.IsBool(dto.DataType);
        if (dto.AlarmEnabled && !isBool && dto.HighHigh is null && dto.High is null && dto.Low is null && dto.LowLow is null)
            return ApiResult.Fail("启用报警后至少配置一个报警阈值（高高/高/低/低低）", "alarm");

        if (dto.HighHigh is not null && dto.High is not null && dto.HighHigh < dto.High)
            return ApiResult.Fail("高高报警阈值不能小于高报警阈值", "highHigh");

        if (dto.LowLow is not null && dto.Low is not null && dto.LowLow > dto.Low)
            return ApiResult.Fail("低低报警阈值不能大于低报警阈值", "lowLow");

        // 7. 同设备下测点名称不允许重复
        var duplicate = await db.Points.AnyAsync(p =>
            p.DeviceId == dto.DeviceId.Value &&
            p.Name == dto.Name.Trim() &&
            p.Id != currentPointId);
        if (duplicate)
            return ApiResult.Fail($"该设备下已存在同名测点：{dto.Name.Trim()}", "name");

        return ApiResult.Ok();
    }

    private static string WidthName(string kind) => kind switch
    {
        "Bool" => "位（Bool）",
        "Word" => "字（2 字节）",
        "DWord" => "双字（4 字节）",
        _ => kind
    };
}

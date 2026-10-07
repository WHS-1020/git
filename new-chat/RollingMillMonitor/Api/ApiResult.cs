namespace RollerMillMonitor.Api;

/// <summary>
/// 统一接口返回格式。
/// 成功：{ success: true,  data: {...} }
/// 失败：{ success: false, message: "请输入 PLC 地址", field: "address" }
/// </summary>
public class ApiResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Field { get; set; }
    public object? Data { get; set; }

    public static ApiResult Ok(object? data = null) => new() { Success = true, Data = data };

    public static ApiResult Fail(string message, string? field = null) =>
        new() { Success = false, Message = message, Field = field };
}

namespace RollerMillMonitor.Dtos;

/// <summary>设备新增/编辑请求</summary>
public class DeviceDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
}

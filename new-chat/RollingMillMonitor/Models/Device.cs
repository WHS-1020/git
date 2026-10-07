namespace RollerMillMonitor.Models;

/// <summary>工艺设备，例如：1#轧机、2#轧机</summary>
public class Device
{
    public int Id { get; set; }

    /// <summary>设备名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>设备编码，例如 ROLLING-01</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>设备说明</summary>
    public string? Description { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>排序</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>该设备下的全部测点</summary>
    public List<Point> Points { get; set; } = new();
}

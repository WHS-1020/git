namespace RollerMillMonitor.Dtos;

/// <summary>PLC 新增/编辑请求</summary>
public class PlcDto
{
    public string Name { get; set; } = string.Empty;
    public string Protocol { get; set; } = "S7";
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; } = 102;
    public int? Rack { get; set; } = 0;
    public int? Slot { get; set; } = 1;
    public bool Enabled { get; set; } = true;
}

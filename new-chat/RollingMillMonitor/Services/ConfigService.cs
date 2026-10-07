using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;
using RollerMillMonitor.Models;
using RollerMillMonitor.Validation;

namespace RollerMillMonitor.Services;

/// <summary>
/// 配置导入导出服务。
/// 导出格式：JSON（完整结构）与 CSV（Section 分节：PLC / Device / Point / AlarmThreshold）。
/// 导入校验：设备名称重复、PLC 是否存在、地址格式、数据类型支持、测点重复，失败返回具体行号与原因（原子导入：有错误则不保存任何数据）。
/// </summary>
public class ConfigService
{
    private static readonly string[] CsvColumns =
    {
        "Section", "Name", "Code", "Protocol", "IPAddress", "Port", "Rack", "Slot", "Enabled",
        "Description", "DeviceName", "PlcName", "Address", "DataType", "Unit", "Scale", "Offset",
        "AlarmEnabled", "HighHigh", "High", "Low", "LowLow", "AlarmDelayMs", "RecoveryDelayMs",
        "Deadband", "SortOrder"
    };

    private readonly ILogger<ConfigService> _logger;

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
    }

    // ---------------- 导出 ----------------

    public async Task<ConfigExportModel> BuildExportAsync(AppDbContext db, CancellationToken ct)
    {
        var plcs = await db.Plcs.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);
        var devices = await db.Devices.AsNoTracking()
            .Include(d => d.Points)
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

        return new ConfigExportModel
        {
            ExportedAt = DateTime.Now,
            Plcs = plcs.Select(p => new PlcExport
            {
                Name = p.Name,
                Protocol = p.Protocol,
                IpAddress = p.IpAddress,
                Port = p.Port,
                Rack = p.Rack,
                Slot = p.Slot,
                Enabled = p.Enabled
            }).ToList(),
            Devices = devices.Select(d => new DeviceExport
            {
                Name = d.Name,
                Code = d.Code,
                Description = d.Description,
                Enabled = d.Enabled,
                SortOrder = d.SortOrder,
                Points = d.Points.OrderBy(p => p.SortOrder).Select(p => new PointExport
                {
                    Name = p.Name,
                    Code = p.Code,
                    PlcName = p.Plc?.Name ?? "",
                    Address = p.Address,
                    DataType = p.DataType,
                    Unit = p.Unit,
                    Scale = p.Scale,
                    Offset = p.Offset,
                    Enabled = p.Enabled,
                    AlarmEnabled = p.AlarmEnabled,
                    HighHigh = p.HighHigh,
                    High = p.High,
                    Low = p.Low,
                    LowLow = p.LowLow,
                    AlarmDelayMs = p.AlarmDelayMs,
                    RecoveryDelayMs = p.RecoveryDelayMs,
                    Deadband = p.Deadband,
                    SortOrder = p.SortOrder
                }).ToList()
            }).ToList(),
            AlarmThresholds = devices.SelectMany(d => d.Points)
                .Where(p => p.AlarmEnabled)
                .Select(p => new AlarmThresholdExport
                {
                    DeviceName = p.Device?.Name ?? "",
                    PointName = p.Name,
                    AlarmEnabled = p.AlarmEnabled,
                    HighHigh = p.HighHigh,
                    High = p.High,
                    Low = p.Low,
                    LowLow = p.LowLow,
                    AlarmDelayMs = p.AlarmDelayMs,
                    RecoveryDelayMs = p.RecoveryDelayMs,
                    Deadband = p.Deadband
                }).ToList()
        };
    }

    public string ToCsv(ConfigExportModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", CsvColumns));

        foreach (var p in model.Plcs)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                "PLC", p.Name, "", p.Protocol, p.IpAddress, p.Port.ToString(),
                p.Rack?.ToString() ?? "", p.Slot?.ToString() ?? "", p.Enabled ? "1" : "0", ""
            }));
        }

        foreach (var d in model.Devices)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                "Device", d.Name, d.Code, "", "", "", "", "", d.Enabled ? "1" : "0",
                d.Description ?? ""
            }));
        }

        foreach (var d in model.Devices)
        {
            foreach (var p in d.Points)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    "Point", p.Name, p.Code, "", "", "", "", "", p.Enabled ? "1" : "0", "",
                    d.Name, p.PlcName, p.Address, p.DataType, p.Unit,
                    p.Scale.ToString("0.########", CultureInfo.InvariantCulture),
                    p.Offset.ToString("0.########", CultureInfo.InvariantCulture),
                    p.AlarmEnabled ? "1" : "0",
                    p.HighHigh?.ToString(CultureInfo.InvariantCulture) ?? "",
                    p.High?.ToString(CultureInfo.InvariantCulture) ?? "",
                    p.Low?.ToString(CultureInfo.InvariantCulture) ?? "",
                    p.LowLow?.ToString(CultureInfo.InvariantCulture) ?? "",
                    p.AlarmDelayMs.ToString(), p.RecoveryDelayMs.ToString(),
                    p.Deadband.ToString("0.########", CultureInfo.InvariantCulture),
                    p.SortOrder.ToString()
                }));
            }
        }

        foreach (var t in model.AlarmThresholds)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                "AlarmThreshold", "", "", "", "", "", "", "", "", "", t.DeviceName, t.PointName,
                "", "", "", "", "", t.AlarmEnabled ? "1" : "0",
                t.HighHigh?.ToString(CultureInfo.InvariantCulture) ?? "",
                t.High?.ToString(CultureInfo.InvariantCulture) ?? "",
                t.Low?.ToString(CultureInfo.InvariantCulture) ?? "",
                t.LowLow?.ToString(CultureInfo.InvariantCulture) ?? "",
                t.AlarmDelayMs.ToString(), t.RecoveryDelayMs.ToString(),
                t.Deadband.ToString("0.########", CultureInfo.InvariantCulture), ""
            }));
        }

        return sb.ToString();
    }

    // ---------------- 导入 ----------------

    public async Task<ImportResult> ImportAsync(
        AppDbContext db, string format, string content, CancellationToken ct)
    {
        try
        {
            if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
                return await ImportCsvAsync(db, content, ct);
            return await ImportJsonAsync(db, content, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "配置导入 JSON 解析失败");
            return new ImportResult
            {
                Success = false,
                Errors = { new ImportError { Line = 0, Message = $"JSON 格式错误：{ex.Message}" } }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "配置导入失败");
            return new ImportResult
            {
                Success = false,
                Errors = { new ImportError { Line = 0, Message = $"导入失败：{ex.Message}" } }
            };
        }
    }

    private async Task<ImportResult> ImportJsonAsync(AppDbContext db, string content, CancellationToken ct)
    {
        var model = JsonSerializer.Deserialize<ConfigExportModel>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new JsonException("内容为空");

        return await ValidateAndApplyAsync(db, model, null, ct);
    }

    private async Task<ImportResult> ImportCsvAsync(AppDbContext db, string content, CancellationToken ct)
    {
        var model = new ConfigExportModel();
        var errors = new List<ImportError>();
        var lineNo = 0;

        foreach (var rawLine in content.Replace("\r\n", "\n").Split('\n'))
        {
            lineNo++;
            var line = rawLine.TrimEnd('\r').Trim();
            if (line.Length == 0) continue;

            var fields = ParseCsvLine(line);
            if (fields.Length != CsvColumns.Length)
            {
                errors.Add(new ImportError { Line = lineNo, Message = $"列数错误：期望 {CsvColumns.Length} 列，实际 {fields.Length} 列" });
                continue;
            }

            var section = fields[0].Trim();
            try
            {
                switch (section.ToUpperInvariant())
                {
                    case "PLC":
                        model.Plcs.Add(new PlcExport
                        {
                            Name = fields[1].Trim(),
                            Protocol = fields[3].Trim(),
                            IpAddress = fields[4].Trim(),
                            Port = ParseInt(fields[5], 102, lineNo, errors, "端口"),
                            Rack = ParseNullableInt(fields[6]),
                            Slot = ParseNullableInt(fields[7]),
                            Enabled = ParseBool(fields[8])
                        });
                        break;
                    case "DEVICE":
                        model.Devices.Add(new DeviceExport
                        {
                            Name = fields[1].Trim(),
                            Code = fields[2].Trim(),
                            Description = fields[9].Trim(),
                            Enabled = ParseBool(fields[8]),
                            SortOrder = ParseInt(fields[25], 0, lineNo, errors, "排序")
                        });
                        break;
                    case "POINT":
                        var point = new PointExport
                        {
                            Name = fields[1].Trim(),
                            Code = fields[2].Trim(),
                            Enabled = ParseBool(fields[8]),
                            PlcName = fields[11].Trim(),
                            Address = fields[12].Trim(),
                            DataType = fields[13].Trim(),
                            Unit = fields[14].Trim(),
                            Scale = ParseDouble(fields[15], 1, lineNo, errors, "倍率"),
                            Offset = ParseDouble(fields[16], 0, lineNo, errors, "偏移"),
                            AlarmEnabled = ParseBool(fields[17]),
                            HighHigh = ParseNullableDouble(fields[18]),
                            High = ParseNullableDouble(fields[19]),
                            Low = ParseNullableDouble(fields[20]),
                            LowLow = ParseNullableDouble(fields[21]),
                            AlarmDelayMs = ParseInt(fields[22], 0, lineNo, errors, "报警延时"),
                            RecoveryDelayMs = ParseInt(fields[23], 0, lineNo, errors, "恢复延时"),
                            Deadband = ParseDouble(fields[24], 0, lineNo, errors, "死区"),
                            SortOrder = ParseInt(fields[25], 0, lineNo, errors, "排序")
                        };
                        var device = model.Devices.FirstOrDefault(d => d.Name == fields[10].Trim());
                        if (device is null)
                        {
                            device = new DeviceExport { Name = fields[10].Trim() };
                            model.Devices.Add(device);
                        }
                        device.Points.Add(point);
                        break;
                    case "ALARMTHRESHOLD":
                        model.AlarmThresholds.Add(new AlarmThresholdExport
                        {
                            DeviceName = fields[10].Trim(),
                            PointName = fields[11].Trim(),
                            AlarmEnabled = ParseBool(fields[17]),
                            HighHigh = ParseNullableDouble(fields[18]),
                            High = ParseNullableDouble(fields[19]),
                            Low = ParseNullableDouble(fields[20]),
                            LowLow = ParseNullableDouble(fields[21]),
                            AlarmDelayMs = ParseInt(fields[22], 0, lineNo, errors, "报警延时"),
                            RecoveryDelayMs = ParseInt(fields[23], 0, lineNo, errors, "恢复延时"),
                            Deadband = ParseDouble(fields[24], 0, lineNo, errors, "死区")
                        });
                        break;
                    default:
                        errors.Add(new ImportError { Line = lineNo, Message = $"未知的分节：{section}" });
                        break;
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ImportError { Line = lineNo, Message = ex.Message });
            }
        }

        if (errors.Count > 0)
            return new ImportResult { Success = false, Errors = errors };

        return await ValidateAndApplyAsync(db, model, lineNo, ct);
    }

    private async Task<ImportResult> ValidateAndApplyAsync(
        AppDbContext db, ConfigExportModel model, int? lineOffset, CancellationToken ct)
    {
        var errors = new List<ImportError>();
        var result = new ImportResult();

        // 现有数据索引
        var existingPlcs = await db.Plcs.ToListAsync(ct);
        var existingDevices = await db.Devices.Include(d => d.Points).ToListAsync(ct);
        var plcByName = existingPlcs.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var deviceByName = existingDevices.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);

        var lineOf = new Func<int, int>(n => n > 0 ? n : (lineOffset ?? 0));

        // ---- 校验 PLC ----
        var newPlcs = new List<Plc>();
        foreach (var p in model.Plcs)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
            { errors.Add(new ImportError { Line = lineOf(0), Message = "PLC 名称不能为空" }); continue; }

            if (!new[] { "S7", "Simulator", "ModbusTcp", "ModbusRtu" }.Contains(p.Protocol, StringComparer.OrdinalIgnoreCase))
            { errors.Add(new ImportError { Line = lineOf(0), Message = $"PLC[{p.Name}] 不支持的协议：{p.Protocol}" }); continue; }

            if (model.Plcs.Count(x => x.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase)) > 1)
            { errors.Add(new ImportError { Line = lineOf(0), Message = $"PLC 名称重复：{p.Name}" }); continue; }

            if (!p.Protocol.Equals("Simulator", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(p.IpAddress))
            { errors.Add(new ImportError { Line = lineOf(0), Message = $"PLC[{p.Name}] 缺少 IP 地址" }); continue; }

            if (p.Port is < 1 or > 65535)
            { errors.Add(new ImportError { Line = lineOf(0), Message = $"PLC[{p.Name}] 端口无效：{p.Port}" }); continue; }

            newPlcs.Add(new Plc
            {
                Name = p.Name,
                Protocol = p.Protocol,
                IpAddress = p.IpAddress,
                Port = p.Port,
                Rack = p.Rack,
                Slot = p.Slot,
                Enabled = p.Enabled
            });
        }

        // ---- 校验设备 ----
        var newDevices = new List<Device>();
        foreach (var d in model.Devices)
        {
            if (string.IsNullOrWhiteSpace(d.Name))
            { errors.Add(new ImportError { Line = lineOf(0), Message = "设备名称不能为空" }); continue; }

            if (model.Devices.Count(x => x.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase)) > 1)
            { errors.Add(new ImportError { Line = lineOf(0), Message = $"设备名称重复：{d.Name}" }); continue; }

            if (deviceByName.ContainsKey(d.Name))
            { errors.Add(new ImportError { Line = lineOf(0), Message = $"设备名称已存在：{d.Name}（请修改后再导入）" }); continue; }

            newDevices.Add(new Device
            {
                Name = d.Name,
                Code = d.Code,
                Description = d.Description,
                Enabled = d.Enabled,
                SortOrder = d.SortOrder
            });
        }

        // ---- 校验测点 ----
        var newPoints = new List<(Device Device, Point Point)>();
        foreach (var d in model.Devices)
        {
            var device = newDevices.FirstOrDefault(x => x.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase));
            if (device is null) continue; // 设备本身已报错

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in d.Points)
            {
                if (string.IsNullOrWhiteSpace(p.Name))
                { errors.Add(new ImportError { Line = lineOf(0), Message = $"设备[{d.Name}] 存在名称为空的测点" }); continue; }

                if (!seenNames.Add(p.Name))
                { errors.Add(new ImportError { Line = lineOf(0), Message = $"设备[{d.Name}] 下测点名称重复：{p.Name}" }); continue; }

                if (!DataTypeRegistry.IsSupported(p.DataType))
                { errors.Add(new ImportError { Line = lineOf(0), Message = $"测点[{d.Name}/{p.Name}] 不支持的数据类型：{p.DataType}" }); continue; }

                Plc? plc = null;
                if (!string.IsNullOrWhiteSpace(p.PlcName))
                {
                    plc = plcByName.GetValueOrDefault(p.PlcName) ?? newPlcs.FirstOrDefault(x => x.Name.Equals(p.PlcName, StringComparison.OrdinalIgnoreCase));
                    if (plc is null)
                    {
                        errors.Add(new ImportError { Line = lineOf(0), Message = $"测点[{d.Name}/{p.Name}] 绑定的 PLC 不存在：{p.PlcName}" });
                        continue;
                    }
                }

                if (!string.IsNullOrWhiteSpace(p.Address))
                {
                    if (plc is null)
                    {
                        errors.Add(new ImportError { Line = lineOf(0), Message = $"测点[{d.Name}/{p.Name}] 填了地址但未绑定 PLC" });
                        continue;
                    }
                    var parsed = AddressParser.Parse(plc.Protocol, p.Address.Trim());
                    if (parsed is null)
                    {
                        errors.Add(new ImportError { Line = lineOf(0), Message = $"测点[{d.Name}/{p.Name}] PLC 地址格式错误：{p.Address}" });
                        continue;
                    }
                    if (!AddressParser.TypeMatches(parsed, p.DataType))
                    {
                        errors.Add(new ImportError { Line = lineOf(0), Message = $"测点[{d.Name}/{p.Name}] 地址 {p.Address} 不支持 {p.DataType} 类型" });
                        continue;
                    }
                }

                newPoints.Add((device, new Point
                {
                    Name = p.Name,
                    Code = p.Code,
                    // 使用导航属性，EF 会在保存时自动回填外键（标量外键指向未保存实体会导致外键约束失败）
                    Device = device,
                    Plc = plc,
                    Address = p.Address,
                    DataType = p.DataType,
                    Unit = p.Unit,
                    Scale = p.Scale,
                    Offset = p.Offset,
                    Enabled = p.Enabled,
                    AlarmEnabled = p.AlarmEnabled,
                    HighHigh = p.HighHigh,
                    High = p.High,
                    Low = p.Low,
                    LowLow = p.LowLow,
                    AlarmDelayMs = p.AlarmDelayMs,
                    RecoveryDelayMs = p.RecoveryDelayMs,
                    Deadband = p.Deadband,
                    SortOrder = p.SortOrder
                }));
            }
        }

        // ---- 校验报警阈值 ----
        var thresholdTargets = new List<(Point Point, AlarmThresholdExport T)>();
        foreach (var t in model.AlarmThresholds)
        {
            var point = newPoints.FirstOrDefault(x =>
                x.Device.Name.Equals(t.DeviceName, StringComparison.OrdinalIgnoreCase) &&
                x.Point.Name.Equals(t.PointName, StringComparison.OrdinalIgnoreCase)).Point;
            if (point is null && deviceByName.TryGetValue(t.DeviceName, out var existDevice))
            {
                point = existDevice.Points.FirstOrDefault(p => p.Name.Equals(t.PointName, StringComparison.OrdinalIgnoreCase));
            }
            if (point is null)
            {
                errors.Add(new ImportError { Line = lineOf(0), Message = $"报警阈值指向的测点不存在：{t.DeviceName}/{t.PointName}" });
                continue;
            }
            thresholdTargets.Add((point, t));
        }

        if (errors.Count > 0)
        {
            result.Success = false;
            result.Errors = errors;
            return result;
        }

        // ---- 全部通过，应用 ----
        foreach (var p in newPlcs)
        {
            if (plcByName.TryGetValue(p.Name, out var existing))
            {
                existing.Protocol = p.Protocol;
                existing.IpAddress = p.IpAddress;
                existing.Port = p.Port;
                existing.Rack = p.Rack;
                existing.Slot = p.Slot;
                existing.Enabled = p.Enabled;
            }
            else
            {
                db.Plcs.Add(p);
                plcByName[p.Name] = p;
            }
            result.PlcCount++;
        }

        foreach (var d in newDevices)
        {
            db.Devices.Add(d);
            deviceByName[d.Name] = d;
            result.DeviceCount++;
        }

        foreach (var (device, point) in newPoints)
        {
            db.Points.Add(point);
            result.PointCount++;
        }

        foreach (var (point, t) in thresholdTargets)
        {
            point.AlarmEnabled = t.AlarmEnabled;
            point.HighHigh = t.HighHigh;
            point.High = t.High;
            point.Low = t.Low;
            point.LowLow = t.LowLow;
            point.AlarmDelayMs = t.AlarmDelayMs;
            point.RecoveryDelayMs = t.RecoveryDelayMs;
            point.Deadband = t.Deadband;
            point.UpdatedAt = DateTime.Now;
            result.ThresholdCount++;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("配置导入成功：PLC {plc} 个、设备 {dev} 个、测点 {point} 个、阈值 {thr} 个",
            result.PlcCount, result.DeviceCount, result.PointCount, result.ThresholdCount);

        result.Success = true;
        return result;
    }

    // ---------------- CSV 工具 ----------------

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    private static int ParseInt(string s, int def, int lineNo, List<ImportError> errors, string label)
    {
        if (string.IsNullOrWhiteSpace(s)) return def;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v;
        errors.Add(new ImportError { Line = lineNo, Message = $"{label} 不是合法整数：{s}" });
        return def;
    }

    private static int? ParseNullableInt(string s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double ParseDouble(string s, double def, int lineNo, List<ImportError> errors, string label)
    {
        if (string.IsNullOrWhiteSpace(s)) return def;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        errors.Add(new ImportError { Line = lineNo, Message = $"{label} 不是合法数字：{s}" });
        return def;
    }

    private static double? ParseNullableDouble(string s)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static bool ParseBool(string s)
        => s is "1" or "true" or "TRUE" or "True";
}

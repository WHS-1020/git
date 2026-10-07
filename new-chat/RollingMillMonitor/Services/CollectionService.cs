using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RollerMillMonitor.Data;
using RollerMillMonitor.Dtos;
using RollerMillMonitor.Hubs;
using RollerMillMonitor.Models;
using RollerMillMonitor.PlcDrivers;
using RollerMillMonitor.Validation;

namespace RollerMillMonitor.Services;

/// <summary>采集服务配置</summary>
public class CollectionOptions
{
    public int IntervalMs { get; set; } = 2000;
    public int TrendRetentionDays { get; set; } = 30;
}

/// <summary>
/// 后台采集服务：
///  1. 读取所有启用的 PLC；
///  2. 找出绑定该 PLC 的启用测点；
///  3. 按地址读取数据、按数据类型解析、应用 倍率×原始值+偏移；
///  4. 保存实时值缓存、更新最后读取时间、更新通信状态、判断报警；
///  5. 通过 SignalR 推送总览快照。
/// 单个测点/单个 PLC 失败不影响其他测点/PLC。
/// </summary>
public class CollectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PlcDriverFactory _driverFactory;
    private readonly RealTimeCache _cache;
    private readonly AlarmEngine _alarmEngine;
    private readonly TrendService _trendService;
    private readonly OverviewService _overview;
    private readonly IHubContext<MonitorHub> _hub;
    private readonly ILogger<CollectionService> _logger;
    private readonly CollectionOptions _options;

    private DateTime _lastCleanup = DateTime.MinValue;

    public CollectionService(
        IServiceScopeFactory scopeFactory,
        PlcDriverFactory driverFactory,
        RealTimeCache cache,
        AlarmEngine alarmEngine,
        TrendService trendService,
        OverviewService overview,
        IHubContext<MonitorHub> hub,
        ILogger<CollectionService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _driverFactory = driverFactory;
        _cache = cache;
        _alarmEngine = alarmEngine;
        _trendService = trendService;
        _overview = overview;
        _hub = hub;
        _logger = logger;

        _options = config.GetSection("Collection").Get<CollectionOptions>() ?? new CollectionOptions();
        if (_options.IntervalMs < 500) _options.IntervalMs = 500;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("后台采集服务启动，采集周期 {interval} ms", _options.IntervalMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "采集周期执行异常，等待下一周期");
            }

            try
            {
                await Task.Delay(_options.IntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("后台采集服务停止");
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 每小时清理一次过期趋势数据
        if (DateTime.Now - _lastCleanup > TimeSpan.FromHours(1))
        {
            _lastCleanup = DateTime.Now;
            try
            {
                await _trendService.CleanupAsync(db, DateTime.Now.AddDays(-_options.TrendRetentionDays), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "趋势数据清理失败（不影响采集）");
            }
        }

        var plcs = await db.Plcs.Where(p => p.Enabled).ToListAsync(ct);
        var points = await db.Points
            .Include(p => p.Device)
            .Include(p => p.Plc)
            .Where(p => p.Enabled && p.Device.Enabled && p.PlcId != null)
            .ToListAsync(ct);

        var trendBatch = new List<TrendRecord>();
        var plcChanged = new List<Plc>();

        foreach (var plc in plcs)
        {
            var group = points.Where(p => p.PlcId == plc.Id).ToList();
            if (group.Count == 0)
                continue;

            IPlcDriver driver;
            try
            {
                driver = _driverFactory.GetOrCreate(plc);
            }
            catch (NotSupportedException ex)
            {
                // 协议未实现：该 PLC 下所有测点标记为配置错误，不影响其他 PLC
                _logger.LogWarning("PLC[{plc}] 协议未实现：{msg}", plc.Name, ex.Message);
                foreach (var p in group)
                {
                    var rt = _cache.GetOrCreate(p.Id);
                    rt.Status = PointStatuses.ConfigError;
                    rt.StatusReason = ex.Message;
                    rt.Value = null;
                    rt.Timestamp = DateTime.Now;
                    rt.ErrorMessage = ex.Message;
                }
                if (plc.ConnectionStatus != "Fault" || plc.LastError != ex.Message)
                {
                    plc.ConnectionStatus = "Fault";
                    plc.LastError = ex.Message;
                    plcChanged.Add(plc);
                }
                await PushPlcStatus(plc);
                continue;
            }

            // 连接 PLC（未连接时）
            if (!driver.IsConnected)
            {
                var connectResult = await driver.ConnectAsync(ct);
                if (!connectResult.Success)
                {
                    // 通信故障：PLC 无法连接
                    _logger.LogWarning("PLC[{plc}]({ip}:{port}) 连接失败：{err}",
                        plc.Name, plc.IpAddress, plc.Port, connectResult.Error);
                    plc.ConnectionStatus = "Fault";
                    plc.LastError = connectResult.Error;
                    plcChanged.Add(plc);

                    foreach (var p in group)
                    {
                        var rtComm = _cache.GetOrCreate(p.Id);
                        rtComm.Status = PointStatuses.CommFault;
                        rtComm.StatusReason = $"PLC 连接失败：{connectResult.Error}";
                        rtComm.Value = null;
                        rtComm.Timestamp = DateTime.Now;
                        rtComm.ErrorMessage = connectResult.Error;
                        rtComm.IsSimulated = plc.IsSimulated;
                        trendBatch.Add(MakeTrend(p, null, PointStatuses.CommFault));
                        await _alarmEngine.EvaluatePointAsync(db, p, PointStatuses.CommFault, null, ct);
                    }
                    await PushPlcStatus(plc);
                    continue;
                }

                plc.ConnectionStatus = "Connected";
                plc.LastError = null;
                plc.LastConnectedAt = DateTime.Now;
                plcChanged.Add(plc);
                _logger.LogInformation("PLC[{plc}] 连接成功", plc.Name);
                await PushPlcStatus(plc);
            }

            // 读取该 PLC 下每个测点（单点失败不影响其他点）
            foreach (var p in group)
            {
                try
                {
                    var (cfgStatus, cfgReason) = PointStatusResolver.ResolveConfig(p, plc);
                    if (cfgStatus != PointStatuses.Normal)
                    {
                        var rtCfg = _cache.GetOrCreate(p.Id);
                        rtCfg.Status = cfgStatus;
                        rtCfg.StatusReason = cfgReason;
                        rtCfg.Value = null;
                        rtCfg.Timestamp = DateTime.Now;
                        rtCfg.IsSimulated = plc.IsSimulated;
                        await _alarmEngine.EvaluatePointAsync(db, p, cfgStatus, null, ct);
                        continue;
                    }

                    var readResult = await driver.ReadAsync(p.Address, p.DataType, ct);
                    var rt = _cache.GetOrCreate(p.Id);

                    if (!readResult.Success)
                    {
                        // 读取失败（PLC 已连接）
                        rt.Status = PointStatuses.ReadFailed;
                        rt.StatusReason = $"读取失败：{readResult.Error}";
                        rt.Value = null;
                        rt.Timestamp = DateTime.Now;
                        rt.ErrorMessage = readResult.Error;
                        rt.IsSimulated = plc.IsSimulated;
                        trendBatch.Add(MakeTrend(p, null, PointStatuses.ReadFailed));
                        await _alarmEngine.EvaluatePointAsync(db, p, PointStatuses.ReadFailed, null, ct);
                    }
                    else
                    {
                        var value = readResult.Value!.Value * p.Scale + p.Offset;
                        rt.Status = PointStatuses.Normal;
                        rt.StatusReason = "正常";
                        rt.Value = Math.Round(value, 4);
                        rt.Timestamp = DateTime.Now;
                        rt.ErrorMessage = null;
                        rt.IsSimulated = plc.IsSimulated;
                        trendBatch.Add(MakeTrend(p, value, PointStatuses.Normal));
                        await _alarmEngine.EvaluatePointAsync(db, p, PointStatuses.Normal, value, ct);
                    }
                }
                catch (Exception ex)
                {
                    // 单个测点异常不中断其他测点
                    _logger.LogError(ex, "测点[{device}/{point}]({address}) 采集异常",
                        p.Device?.Name, p.Name, p.Address);
                    var rt = _cache.GetOrCreate(p.Id);
                    rt.Status = PointStatuses.ReadFailed;
                    rt.StatusReason = $"采集异常：{ex.Message}";
                    rt.Timestamp = DateTime.Now;
                    rt.ErrorMessage = ex.Message;
                    rt.IsSimulated = plc.IsSimulated;
                }
            }
        }

        if (plcChanged.Count > 0)
            await db.SaveChangesAsync(ct);

        await _trendService.SaveBatchAsync(db, trendBatch, ct);

        // 推送总览快照
        try
        {
            var overview = await _overview.BuildAsync(db, ct);
            await _hub.Clients.All.SendAsync("Snapshot", overview, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "推送总览快照失败");
        }
    }

    private static TrendRecord MakeTrend(Point p, double? value, string status) => new()
    {
        PointId = p.Id,
        PointName = p.Name,
        DeviceId = p.DeviceId,
        DeviceName = p.Device?.Name ?? "",
        Value = value,
        Timestamp = DateTime.Now,
        Status = status
    };

    private async Task PushPlcStatus(Plc plc)
    {
        try
        {
            await _hub.Clients.All.SendAsync("PlcStatusChanged", new OverviewPlc
            {
                Id = plc.Id,
                Name = plc.Name,
                Protocol = plc.Protocol,
                IpAddress = plc.IpAddress,
                Port = plc.Port,
                Enabled = plc.Enabled,
                IsSimulated = plc.IsSimulated,
                ConnectionStatus = plc.ConnectionStatus,
                LastConnectedAt = plc.LastConnectedAt,
                LastError = plc.LastError
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "推送 PLC 状态失败");
        }
    }
}

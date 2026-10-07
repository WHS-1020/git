using Microsoft.AspNetCore.SignalR;

namespace RollerMillMonitor.Hubs;

/// <summary>
/// 实时监控 Hub。服务端广播方法：
///   Snapshot(overviewDto)   —— 总览快照（每采集周期推送）
///   AlarmRaised(alarmDto)   —— 报警产生
///   AlarmRecovered(alarmDto)—— 报警恢复
///   AlarmUpdated(alarmDto)  —— 报警确认/取消确认等变更
/// </summary>
public class MonitorHub : Hub
{
}

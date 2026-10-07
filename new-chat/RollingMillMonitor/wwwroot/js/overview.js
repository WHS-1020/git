/* 总览页面：统计卡片 + PLC 状态 + 设备卡片 + 当前报警（SignalR 实时刷新） */

function renderOverview(dto) {
  const c = dto.counts;
  setText('st-devices', c.devices);
  setText('st-points', c.points);
  setText('st-normal', c.normal);
  setText('st-notconfigured', c.notConfigured);
  setText('st-configerror', c.configError);
  setText('st-commfault', c.commFault);
  setText('st-readfailed', c.readFailed);
  setText('st-disabled', c.disabled);
  setText('st-alarms', c.activeAlarms);

  // 顶部徽标动态化：全部真实 PLC 数据时隐藏"模拟"标记
  const badge = document.getElementById('sim-badge');
  if (badge) {
    const hasSim = dto.devices.some(d => d.points.some(p => p.isSimulated))
      || dto.plcs.some(p => p.isSimulated);
    if (hasSim) { badge.style.display = ''; badge.textContent = '含模拟数据'; }
    else { badge.style.display = 'none'; }
  }

  // PLC 状态
  const plcBox = document.getElementById('plc-list');
  if (!dto.plcs.length) {
    plcBox.innerHTML = '<div class="empty">暂无 PLC，请到 PLC 管理页面新增</div>';
  } else {
    plcBox.innerHTML = `
      <div class="tbl-wrap"><table class="tbl">
        <thead><tr>
          <th>PLC 名称</th><th>协议</th><th>地址</th><th>连接状态</th><th>最后连接</th><th>最后错误</th>
        </tr></thead>
        <tbody>${dto.plcs.map(p => `
          <tr>
            <td>${escapeHtml(p.name)} ${p.isSimulated ? '<span class="sim-badge">模拟</span>' : ''}</td>
            <td>${escapeHtml(p.protocol)}</td>
            <td class="mono">${escapeHtml(p.ipAddress)}:${p.port}</td>
            <td>${connBadge(p.connectionStatus, p.enabled)}</td>
            <td class="small muted">${fmtTime(p.lastConnectedAt)}</td>
            <td class="small reason">${escapeHtml(p.lastError || '—')}</td>
          </tr>`).join('')}
        </tbody>
      </table></div>`;
  }

  // 设备卡片
  const grid = document.getElementById('device-grid');
  if (!dto.devices.length) {
    grid.innerHTML = '<div class="empty">暂无设备，请到设备管理页面新增</div>';
  } else {
    grid.innerHTML = dto.devices.map(d => deviceCard(d)).join('');
  }

  // 当前报警
  const alarmBox = document.getElementById('alarm-list');
  if (!dto.activeAlarms.length) {
    alarmBox.innerHTML = '<div class="empty">暂无活动报警</div>';
  } else {
    alarmBox.innerHTML = `
      <div class="tbl-wrap"><table class="tbl">
        <thead><tr><th>级别</th><th>设备</th><th>测点</th><th>报警内容</th><th>当前值</th><th>阈值</th><th>发生时间</th><th>确认</th></tr></thead>
        <tbody>${dto.activeAlarms.slice(0, 50).map(a => `
          <tr>
            <td>${alarmLevelBadge(a.alarmLevel)}</td>
            <td>${escapeHtml(a.deviceName)}</td>
            <td>${escapeHtml(a.pointName)}</td>
            <td class="small">${escapeHtml(a.message)}</td>
            <td class="num">${a.alarmType === 'Fault' ? (Number(a.alarmValue) === 1 ? '<b class="ok-val">正常</b>' : '<b class="alert-lv-critical">故障</b>') : fmtVal(a.alarmValue) + ' ' + escapeHtml(a.unit)}</td>
            <td class="num muted">${a.alarmType === 'Fault' ? '—' : fmtVal(a.threshold) + ' ' + escapeHtml(a.unit)}</td>
            <td class="small muted">${fmtTime(a.alarmTime)}</td>
            <td class="small">${a.isAcknowledged ? '已确认' : '未确认'}</td>
          </tr>`).join('')}
        </tbody>
      </table></div>`;
  }
}

function connBadge(status, enabled) {
  if (!enabled) return '<span class="badge st-disabled"><span class="dot"></span>已禁用</span>';
  const map = {
    Connected: ['st-normal', '已连接'],
    Fault: ['st-commfault', '通信故障'],
    Unknown: ['st-notconfigured', '未知'],
    Disconnected: ['st-readfailed', '未连接']
  };
  const [cls, name] = map[status] || ['st-notconfigured', status];
  return `<span class="badge ${cls}"><span class="dot"></span>${name}</span>`;
}

function deviceCard(d) {
  const rows = d.points.map(p => {
    // Bool 开关量状态测点（油流/油压）：显示"正常/故障"，不显示数字模拟量
    const val = p.value === null || p.value === undefined
      ? '<span class="muted">—</span>'
      : isBoolType(p.dataType)
        ? boolStateHtml(p)
        : `<span class="big-val ${p.activeAlarmLevel ? 'alert-lv-' + p.activeAlarmLevel.toLowerCase() : ''}">${fmtVal(p.value)}</span><span class="unit">${escapeHtml(p.unit)}</span>`;
    return `<tr>
      <td>${escapeHtml(p.name)} ${p.isSimulated ? '<span class="sim-badge">模拟</span>' : ''}</td>
      <td class="num">${val}</td>
      <td>${statusBadge(p.status, p.statusName)}</td>
      <td class="small reason">${escapeHtml(p.statusReason)}</td>
    </tr>`;
  }).join('');

  const worst = d.points.find(p => p.status === 'CommFault') ? 'st-commfault'
    : d.points.find(p => p.status === 'ReadFailed') ? 'st-readfailed'
    : d.points.find(p => p.status === 'ConfigError') ? 'st-configerror'
    : d.points.find(p => p.status === 'NotConfigured') ? 'st-notconfigured'
    : d.points.find(p => p.status === 'Waiting') ? 'st-waiting'
    : d.points.find(p => p.status === 'Disabled') ? 'st-disabled'
    : 'st-normal';

  return `
    <div class="device-card">
      <div class="dc-head">
        <div>
          <div class="dc-name">${escapeHtml(d.name)} <span class="badge ${worst}"><span class="dot"></span>${escapeHtml(d.commStatusName)}</span></div>
          <div class="dc-code">${escapeHtml(d.code || '')}</div>
        </div>
        <div class="last-update">最后更新：${fmtTime(d.lastUpdate)}</div>
      </div>
      <div class="tbl-wrap"><table class="tbl">
        <thead><tr><th>测点</th><th>实时值</th><th>状态</th><th>说明</th></tr></thead>
        <tbody>${rows}</tbody>
      </table></div>
    </div>`;
}

function setText(id, v) {
  const el = document.getElementById(id);
  if (el) el.textContent = v;
}

/* ---------- 初始化 ---------- */
renderLayout('总览');

// 先拉一次快照，随后由 SignalR 实时刷新
apiGet('/api/overview')
  .then(renderOverview)
  .catch(e => toast(e.message, 'err'));

// 断线兜底：SignalR 未连接/断开时每 5 秒轮询一次，保证页面数据始终实时刷新
let pollTimer = null;
function syncOverviewPolling(connected) {
  if (connected) {
    if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
    return;
  }
  if (!pollTimer) {
    pollTimer = setInterval(() => {
      apiGet('/api/overview').then(renderOverview).catch(() => { /* 静默，等待恢复 */ });
    }, 5000);
  }
}
onSignalrState(syncOverviewPolling);
syncOverviewPolling(signalrConnected);

connectSignalr({
  Snapshot: (dto) => renderOverview(dto),
  AlarmRaised: () => toast('新报警产生，请查看报警管理', 'warn'),
  AlarmUpdated: () => { /* 报警列表由 Snapshot 刷新 */ },
  PlcStatusChanged: () => { /* 总览由 Snapshot 刷新 */ }
});

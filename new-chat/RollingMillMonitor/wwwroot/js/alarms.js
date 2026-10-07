/* 报警管理页面 */

let alarms = [];
let devices = [];

async function loadAlarms() {
  try {
    const qs = new URLSearchParams();
    const deviceId = document.getElementById('f-device').value;
    const type = document.getElementById('f-type').value;
    const active = document.getElementById('f-active').value;
    const ack = document.getElementById('f-ack').value;
    const from = document.getElementById('f-from').value;
    const to = document.getElementById('f-to').value;
    if (deviceId) qs.set('deviceId', deviceId);
    if (type) qs.set('alarmType', type);
    if (active) qs.set('isActive', active);
    if (ack) qs.set('isAcknowledged', ack);
    if (from) qs.set('from', toLocalIso(new Date(from)));
    if (to) qs.set('to', toLocalIso(new Date(to)));
    alarms = await apiGet('/api/alarms?' + qs.toString());
    renderAlarms();
  } catch (e) {
    toast(e.message, 'err');
  }
}

function renderAlarms() {
  const tb = document.getElementById('alarm-tbody');
  if (!alarms.length) {
    tb.innerHTML = '<tr><td colspan="11" class="empty">暂无报警记录</td></tr>';
    return;
  }
  tb.innerHTML = alarms.map(a => `
    <tr>
      <td>${alarmLevelBadge(a.alarmLevel)}</td>
      <td>${escapeHtml(a.deviceName || '—')}</td>
      <td>${escapeHtml(a.pointName)}</td>
      <td class="small">${escapeHtml(a.message)}</td>
      <td class="num">${a.alarmType === 'Fault' ? (Number(a.alarmValue) === 1 ? '<b class="ok-val">正常</b>' : '<b class="alert-lv-critical">故障</b>') : fmtVal(a.alarmValue) + ' ' + escapeHtml(a.unit)}</td>
      <td class="num muted">${a.alarmType === 'Fault' ? '—' : fmtVal(a.threshold) + ' ' + escapeHtml(a.unit)}</td>
      <td class="small muted">${fmtTime(a.alarmTime)}</td>
      <td class="num muted">${fmtDur(a.durationSeconds)}</td>
      <td>${a.isAcknowledged ? '<span class="badge st-normal"><span class="dot"></span>已确认</span>' : '<span class="badge st-configerror"><span class="dot"></span>未确认</span>'}</td>
      <td>${a.isActive ? '<span class="badge st-commfault"><span class="dot"></span>活动中</span>' : '<span class="badge st-normal"><span class="dot"></span>已恢复 ' + fmtTime(a.recoveryTime) + '</span>'}</td>
      <td class="nowrap">
        ${a.isAcknowledged
          ? `<button class="btn sm" onclick="unackAlarm(${a.id})">取消确认</button>`
          : `<button class="btn sm primary" onclick="ackAlarm(${a.id})">确认</button>`}
      </td>
    </tr>`).join('');
}

async function ackAlarm(id) {
  try {
    await apiPost(`/api/alarms/${id}/ack`);
    toast('报警已确认');
    loadAlarms();
  } catch (e) {
    toast(e.message, 'err');
  }
}

async function unackAlarm(id) {
  try {
    await apiPost(`/api/alarms/${id}/unack`);
    toast('已取消确认');
    loadAlarms();
  } catch (e) {
    toast(e.message, 'err');
  }
}

function resetFilters() {
  document.getElementById('f-device').value = '';
  document.getElementById('f-type').value = '';
  document.getElementById('f-active').value = '';
  document.getElementById('f-ack').value = '';
  document.getElementById('f-from').value = '';
  document.getElementById('f-to').value = '';
  loadAlarms();
}

renderLayout('报警管理');

apiGet('/api/devices').then(ds => {
  devices = ds;
  const sel = document.getElementById('f-device');
  sel.innerHTML = '<option value="">全部设备</option>' + ds.map(d =>
    `<option value="${d.id}">${escapeHtml(d.name)}</option>`).join('');
}).catch(e => toast(e.message, 'err'));

loadAlarms();

// SignalR：新报警/恢复/更新时刷新列表
connectSignalr({
  AlarmRaised: () => { loadAlarms(); toast('新报警产生！', 'warn'); },
  AlarmRecovered: () => loadAlarms(),
  AlarmUpdated: () => loadAlarms()
});

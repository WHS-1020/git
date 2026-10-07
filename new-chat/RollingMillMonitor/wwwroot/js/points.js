/* 测点管理页面 */

let points = [];
let plcs = [];
let devices = [];
let editingId = null;

async function loadMeta() {
  try {
    [plcs, devices] = await Promise.all([apiGet('/api/plcs'), apiGet('/api/devices')]);
    fillSelect('f-device', devices, '全部设备', 'id', 'name', true);
    fillSelect('f-plc', plcs, '全部 PLC', 'id', 'name', true);
    fillSelect('pt-device', devices, null, 'id', 'name', false);
    fillSelect('pt-plc', plcs, null, 'id', 'name', false);
    // 从设备管理页跳转过来的参数
    const params = new URLSearchParams(location.search);
    if (params.get('deviceId')) {
      document.getElementById('f-device').value = params.get('deviceId');
    }
    await loadPoints();
  } catch (e) {
    toast(e.message, 'err');
  }
}

function fillSelect(selId, list, placeholder, valKey, textKey, withEmpty) {
  const sel = document.getElementById(selId);
  let html = '';
  if (withEmpty) html += `<option value="">${placeholder || '全部'}</option>`;
  if (!list.length) html += '<option value="">（暂无数据）</option>';
  list.forEach(x => {
    html += `<option value="${x[valKey]}">${escapeHtml(x[textKey])}${x.isSimulated ? '（模拟）' : ''}</option>`;
  });
  sel.innerHTML = html;
}

async function loadPoints(silent = false) {
  try {
    const deviceId = document.getElementById('f-device').value;
    const plcId = document.getElementById('f-plc').value;
    const keyword = document.getElementById('f-keyword').value.trim();
    const qs = new URLSearchParams();
    if (deviceId) qs.set('deviceId', deviceId);
    if (plcId) qs.set('plcId', plcId);
    if (keyword) qs.set('keyword', keyword);
    points = await apiGet('/api/points' + (qs.toString() ? '?' + qs.toString() : ''));
    renderPoints();
  } catch (e) {
    if (!silent) toast(e.message, 'err');
  }
}

function renderPoints() {
  const tb = document.getElementById('point-tbody');
  if (!points.length) {
    tb.innerHTML = '<tr><td colspan="10" class="empty">暂无测点，点击右上角新增</td></tr>';
    return;
  }
  tb.innerHTML = points.map(p => {
    // Bool 开关量状态测点（油流/油压）：显示 正常/故障，不显示数字模拟量
    const curVal = p.value === null || p.value === undefined
      ? '<span class="muted">—</span>'
      : isBoolType(p.dataType)
        ? `<b class="${Number(p.value) === 1 ? 'ok-val' : 'alert-lv-critical'}">${Number(p.value) === 1 ? '正常' : '故障'}</b>`
        : `<b>${fmtVal(p.value)}</b> <span class="muted small">${escapeHtml(p.unit)}</span>`;
    return `
    <tr>
      <td>${escapeHtml(p.deviceName)}</td>
      <td><b>${escapeHtml(p.name)}</b> ${p.isSimulated ? '<span class="sim-badge">模拟</span>' : ''}</td>
      <td>${escapeHtml(p.plcName || '—')}</td>
      <td class="mono">${escapeHtml(p.address || '—')}</td>
      <td class="mono muted">${escapeHtml(p.dataType)}</td>
      <td>${escapeHtml(p.unit)}</td>
      <td class="num">${curVal}</td>
      <td>${statusBadge(p.status, p.statusName)}</td>
      <td class="small muted">${fmtTime(p.timestamp)}</td>
      <td class="nowrap">
        <button class="btn sm" onclick="readNow(${p.id})">立即读取</button>
        <button class="btn sm" onclick="editPoint(${p.id})">编辑</button>
        <button class="btn sm" onclick="togglePoint(${p.id})">${p.enabled ? '停用' : '启用'}</button>
        <button class="btn sm danger" onclick="deletePoint(${p.id})">删除</button>
      </td>
    </tr>`;
  }).join('');
}

function openPointModal() {
  editingId = null;
  document.getElementById('point-modal-title').textContent = '新增测点';
  document.getElementById('point-form').reset();
  document.getElementById('pt-scale').value = 1;
  document.getElementById('pt-offset').value = 0;
  document.getElementById('pt-sort').value = 0;
  document.getElementById('pt-enabled').checked = true;
  document.getElementById('pt-alarm-enabled').checked = false;
  document.getElementById('pt-datatype').value = 'Real';
  updateAddressHint();
  openModal('point-modal');
}

function editPoint(id) {
  const p = points.find(x => x.id === id);
  if (!p) return;
  editingId = id;
  document.getElementById('point-modal-title').textContent = '编辑测点：' + p.name;
  document.getElementById('pt-device').value = p.deviceId;
  document.getElementById('pt-name').value = p.name;
  document.getElementById('pt-code').value = p.code;
  document.getElementById('pt-plc').value = p.plcId || '';
  document.getElementById('pt-address').value = p.address;
  document.getElementById('pt-datatype').value = p.dataType;
  document.getElementById('pt-unit').value = p.unit;
  document.getElementById('pt-scale').value = p.scale;
  document.getElementById('pt-offset').value = p.offset;
  document.getElementById('pt-sort').value = p.sortOrder;
  document.getElementById('pt-enabled').checked = p.enabled;
  document.getElementById('pt-alarm-enabled').checked = p.alarmEnabled;
  document.getElementById('pt-hh').value = p.highHigh ?? '';
  document.getElementById('pt-high').value = p.high ?? '';
  document.getElementById('pt-low').value = p.low ?? '';
  document.getElementById('pt-ll').value = p.lowLow ?? '';
  document.getElementById('pt-alarm-delay').value = p.alarmDelayMs || 0;
  document.getElementById('pt-recovery-delay').value = p.recoveryDelayMs || 0;
  document.getElementById('pt-deadband').value = p.deadband || 0;
  updateAddressHint();
  openModal('point-modal');
}

function updateAddressHint() {
  const plcId = document.getElementById('pt-plc').value;
  const plc = plcs.find(x => x.id == plcId);
  const hint = document.getElementById('pt-address-hint');
  if (plc && plc.protocol === 'Simulator') {
    hint.textContent = '模拟采集器地址：SIM-001 / SIM-002 …（数据将明确标记为模拟）';
  } else if (plc && (plc.protocol === 'ModbusTcp' || plc.protocol === 'ModbusRtu')) {
    hint.textContent = 'Modbus 协议地址格式待驱动实现后确认';
  } else {
    hint.textContent = 'S7 格式：DB1.DBD0（4字节）/ DB1.DBW0（2字节）/ DB1.DBX0.0（位）/ MW10 / MD20；不同类型对应不同地址宽度';
  }
  // Bool 开关量状态测点：隐藏数值阈值，提示 0=故障自动报警
  const isBool = isBoolType(document.getElementById('pt-datatype').value);
  const boolHint = document.getElementById('pt-bool-hint');
  if (boolHint) boolHint.style.display = isBool ? '' : 'none';
  ['pt-hh', 'pt-high', 'pt-low', 'pt-ll'].forEach(id => {
    const el = document.getElementById(id);
    if (el) el.closest('.field').style.display = isBool ? 'none' : '';
  });
}

async function savePoint() {
  const num = (id, def = null) => {
    const v = document.getElementById(id).value;
    return v === '' ? def : Number(v);
  };
  const dto = {
    deviceId: parseInt(document.getElementById('pt-device').value) || null,
    name: document.getElementById('pt-name').value,
    code: document.getElementById('pt-code').value,
    plcId: parseInt(document.getElementById('pt-plc').value) || null,
    address: document.getElementById('pt-address').value,
    dataType: document.getElementById('pt-datatype').value,
    unit: document.getElementById('pt-unit').value,
    scale: num('pt-scale', 1),
    offset: num('pt-offset', 0),
    sortOrder: num('pt-sort', 0),
    enabled: document.getElementById('pt-enabled').checked,
    alarmEnabled: document.getElementById('pt-alarm-enabled').checked,
    highHigh: num('pt-hh'),
    high: num('pt-high'),
    low: num('pt-low'),
    lowLow: num('pt-ll'),
    alarmDelayMs: num('pt-alarm-delay', 0),
    recoveryDelayMs: num('pt-recovery-delay', 0),
    deadband: num('pt-deadband', 0)
  };
  const form = document.getElementById('point-form');
  try {
    if (editingId === null) {
      await apiPost('/api/points', dto);
      toast('测点新增成功');
    } else {
      await apiPut('/api/points/' + editingId, dto);
      toast('测点编辑成功');
    }
    closeModal('point-modal');
    loadPoints();
  } catch (e) {
    showFieldErrors(form, e);
    toast(e.message, 'err');
  }
}

async function readNow(id) {
  const p = points.find(x => x.id === id);
  if (!p) return;
  try {
    const r = await apiPost(`/api/points/${id}/read`);
    if (r.success) {
      const disp = isBoolType(p.dataType)
        ? (Number(r.value) === 1 ? '正常' : '故障')
        : `${fmtVal(r.value)} ${r.unit || ''}`;
      toast(`「${p.name}」读取成功：${disp}${r.isSimulated ? '（模拟）' : ''}`);
    }
    else toast(`「${p.name}」${r.message}`, 'warn');
    loadPoints();
  } catch (e) {
    toast(e.message, 'err');
  }
}

async function togglePoint(id) {
  const p = points.find(x => x.id === id);
  if (!p) return;
  try {
    await apiPut('/api/points/' + id, {
      deviceId: p.deviceId, name: p.name, code: p.code, plcId: p.plcId,
      address: p.address, dataType: p.dataType, unit: p.unit,
      scale: p.scale, offset: p.offset, sortOrder: p.sortOrder,
      enabled: !p.enabled, alarmEnabled: p.alarmEnabled,
      highHigh: p.highHigh, high: p.high, low: p.low, lowLow: p.lowLow,
      alarmDelayMs: p.alarmDelayMs, recoveryDelayMs: p.recoveryDelayMs, deadband: p.deadband
    });
    toast(p.enabled ? '测点已停用' : '测点已启用');
    loadPoints();
  } catch (e) {
    toast(e.message, 'err');
  }
}

async function deletePoint(id) {
  const p = points.find(x => x.id === id);
  if (!confirmAction(`确定删除测点「${p ? p.name : id}」吗？\n相关报警与历史趋势数据将一并删除！`)) return;
  try {
    await apiDelete('/api/points/' + id);
    toast('测点已删除');
    loadPoints();
  } catch (e) {
    toast(e.message, 'err');
  }
}

renderLayout('测点管理');
bindModalClose('point-modal');
document.getElementById('point-save-btn').addEventListener('click', savePoint);
document.getElementById('pt-plc').addEventListener('change', updateAddressHint);
document.getElementById('pt-datatype').addEventListener('change', updateAddressHint);
document.getElementById('f-keyword').addEventListener('keydown', (e) => { if (e.key === 'Enter') loadPoints(); });
loadMeta();

// 实时刷新：每 5 秒自动重新拉取列表（静默失败，不打扰操作）
setInterval(() => loadPoints(true), 5000);

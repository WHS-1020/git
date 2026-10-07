/* PLC 管理页面 */

let plcs = [];
let editingId = null;

async function loadPlcs() {
  try {
    plcs = await apiGet('/api/plcs');
    renderPlcs();
  } catch (e) {
    toast(e.message, 'err');
  }
}

function renderPlcs() {
  const tb = document.getElementById('plc-tbody');
  if (!plcs.length) {
    tb.innerHTML = '<tr><td colspan="11" class="empty">暂无 PLC，点击右上角新增</td></tr>';
    return;
  }
  tb.innerHTML = plcs.map(p => `
    <tr>
      <td>${escapeHtml(p.name)} ${p.isSimulated ? '<span class="sim-badge">模拟</span>' : ''}</td>
      <td class="mono">${escapeHtml(p.protocol)}</td>
      <td class="mono">${escapeHtml(p.ipAddress)}</td>
      <td class="num">${p.port}</td>
      <td class="num">${p.rack ?? '—'}</td>
      <td class="num">${p.slot ?? '—'}</td>
      <td>${connBadge(p)}</td>
      <td>${p.enabled ? '<span class="badge st-normal"><span class="dot"></span>启用</span>' : '<span class="badge st-disabled"><span class="dot"></span>停用</span>'}</td>
      <td class="small muted">${fmtTime(p.lastConnectedAt)}</td>
      <td class="small reason">${escapeHtml(p.lastError || '—')}</td>
      <td class="nowrap">
        <button class="btn sm" onclick="testPlc(${p.id})">测试连接</button>
        <button class="btn sm" onclick="editPlc(${p.id})">编辑</button>
        <button class="btn sm" onclick="togglePlc(${p.id})">${p.enabled ? '停用' : '启用'}</button>
        <button class="btn sm danger" onclick="deletePlc(${p.id})">删除</button>
      </td>
    </tr>`).join('');
}

function connBadge(p) {
  if (!p.enabled) return '<span class="badge st-disabled"><span class="dot"></span>已禁用</span>';
  const map = {
    Connected: ['st-normal', '已连接'],
    Fault: ['st-commfault', '通信故障'],
    Unknown: ['st-notconfigured', '未知'],
    Disconnected: ['st-readfailed', '未连接']
  };
  const [cls, name] = map[p.connectionStatus] || ['st-notconfigured', p.connectionStatus || '未知'];
  return `<span class="badge ${cls}"><span class="dot"></span>${name}</span>`;
}

function openPlcModal() {
  editingId = null;
  document.getElementById('plc-modal-title').textContent = '新增 PLC';
  document.getElementById('plc-form').reset();
  document.getElementById('plc-protocol').value = 'S7';
  document.getElementById('plc-port').value = 102;
  document.getElementById('plc-rack').value = 0;
  document.getElementById('plc-slot').value = 1;
  document.getElementById('plc-enabled').checked = true;
  openModal('plc-modal');
}

function editPlc(id) {
  const p = plcs.find(x => x.id === id);
  if (!p) return;
  editingId = id;
  document.getElementById('plc-modal-title').textContent = '编辑 PLC：' + p.name;
  document.getElementById('plc-name').value = p.name;
  document.getElementById('plc-protocol').value = p.protocol;
  document.getElementById('plc-ip').value = p.ipAddress;
  document.getElementById('plc-port').value = p.port;
  document.getElementById('plc-rack').value = p.rack ?? 0;
  document.getElementById('plc-slot').value = p.slot ?? 1;
  document.getElementById('plc-enabled').checked = p.enabled;
  openModal('plc-modal');
}

async function savePlc() {
  const dto = {
    name: document.getElementById('plc-name').value,
    protocol: document.getElementById('plc-protocol').value,
    ipAddress: document.getElementById('plc-ip').value,
    port: parseInt(document.getElementById('plc-port').value) || 102,
    rack: parseInt(document.getElementById('plc-rack').value) || 0,
    slot: parseInt(document.getElementById('plc-slot').value) || 1,
    enabled: document.getElementById('plc-enabled').checked
  };
  const form = document.getElementById('plc-form');
  try {
    if (editingId === null) {
      await apiPost('/api/plcs', dto);
      toast('PLC 新增成功');
    } else {
      await apiPut('/api/plcs/' + editingId, dto);
      toast('PLC 编辑成功');
    }
    closeModal('plc-modal');
    loadPlcs();
  } catch (e) {
    showFieldErrors(form, e);
    toast(e.message, 'err');
  }
}

async function testPlc(id) {
  const btn = event.target;
  btn.disabled = true;
  btn.textContent = '测试中…';
  try {
    const r = await apiPost(`/api/plcs/${id}/test`);
    if (r.success) toast(`连接成功（${r.latencyMs}ms）`);
    else toast(`连接失败：${r.message}`, 'err');
    loadPlcs();
  } catch (e) {
    toast(e.message, 'err');
  } finally {
    btn.disabled = false;
    btn.textContent = '测试连接';
  }
}

async function togglePlc(id) {
  const p = plcs.find(x => x.id === id);
  if (!p) return;
  try {
    await apiPut('/api/plcs/' + id, {
      name: p.name, protocol: p.protocol, ipAddress: p.ipAddress,
      port: p.port, rack: p.rack, slot: p.slot, enabled: !p.enabled
    });
    toast(p.enabled ? 'PLC 已停用' : 'PLC 已启用');
    loadPlcs();
  } catch (e) {
    toast(e.message, 'err');
  }
}

async function deletePlc(id) {
  const p = plcs.find(x => x.id === id);
  if (!confirmAction(`确定删除 PLC「${p ? p.name : id}」吗？\n其下测点将解除绑定并显示为"未配置"。`)) return;
  try {
    await apiDelete('/api/plcs/' + id);
    toast('PLC 已删除');
    loadPlcs();
  } catch (e) {
    toast(e.message, 'err');
  }
}

renderLayout('PLC 管理');
bindModalClose('plc-modal');
document.getElementById('plc-save-btn').addEventListener('click', savePlc);
document.getElementById('plc-form').addEventListener('keydown', (e) => {
  if (e.key === 'Enter') { e.preventDefault(); savePlc(); }
});
loadPlcs();

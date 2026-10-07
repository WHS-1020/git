/* 设备管理页面 */

let devices = [];
let editingId = null;

async function loadDevices() {
  try {
    devices = await apiGet('/api/devices');
    renderDevices();
  } catch (e) {
    toast(e.message, 'err');
  }
}

function renderDevices() {
  const tb = document.getElementById('device-tbody');
  if (!devices.length) {
    tb.innerHTML = '<tr><td colspan="7" class="empty">暂无设备，点击右上角新增</td></tr>';
    return;
  }
  tb.innerHTML = devices.map(d => `
    <tr>
      <td><b>${escapeHtml(d.name)}</b></td>
      <td class="mono muted">${escapeHtml(d.code)}</td>
      <td class="small muted">${escapeHtml(d.description || '—')}</td>
      <td class="num">${d.pointCount}</td>
      <td>${d.enabled ? '<span class="badge st-normal"><span class="dot"></span>启用</span>' : '<span class="badge st-disabled"><span class="dot"></span>已禁用</span>'}</td>
      <td>${statusBadge(d.status, d.statusName)}</td>
      <td class="nowrap">
        <button class="btn sm" onclick="goPoints(${d.id})">测点管理</button>
        <button class="btn sm" onclick="editDevice(${d.id})">编辑</button>
        <button class="btn sm" onclick="copyDevice(${d.id})">复制配置</button>
        <button class="btn sm" onclick="toggleDevice(${d.id})">${d.enabled ? '禁用' : '启用'}</button>
        <button class="btn sm danger" onclick="deleteDevice(${d.id})">删除</button>
      </td>
    </tr>`).join('');
}

function goPoints(deviceId) {
  location.href = 'points.html?deviceId=' + deviceId;
}

function openDeviceModal() {
  editingId = null;
  document.getElementById('device-modal-title').textContent = '新增设备';
  document.getElementById('device-form').reset();
  document.getElementById('dev-sort').value = 0;
  document.getElementById('dev-enabled').checked = true;
  openModal('device-modal');
}

function editDevice(id) {
  const d = devices.find(x => x.id === id);
  if (!d) return;
  editingId = id;
  document.getElementById('device-modal-title').textContent = '编辑设备：' + d.name;
  document.getElementById('dev-name').value = d.name;
  document.getElementById('dev-code').value = d.code;
  document.getElementById('dev-desc').value = d.description || '';
  document.getElementById('dev-sort').value = d.sortOrder;
  document.getElementById('dev-enabled').checked = d.enabled;
  openModal('device-modal');
}

async function saveDevice() {
  const dto = {
    name: document.getElementById('dev-name').value,
    code: document.getElementById('dev-code').value,
    description: document.getElementById('dev-desc').value,
    sortOrder: parseInt(document.getElementById('dev-sort').value) || 0,
    enabled: document.getElementById('dev-enabled').checked
  };
  const form = document.getElementById('device-form');
  try {
    if (editingId === null) {
      await apiPost('/api/devices', dto);
      toast('设备新增成功');
    } else {
      await apiPut('/api/devices/' + editingId, dto);
      toast('设备编辑成功');
    }
    closeModal('device-modal');
    loadDevices();
  } catch (e) {
    showFieldErrors(form, e);
    toast(e.message, 'err');
  }
}

async function copyDevice(id) {
  const d = devices.find(x => x.id === id);
  if (!confirmAction(`复制设备「${d ? d.name : id}」的配置吗？\n测点名称/单位/数据类型/报警阈值/排序会被复制，PLC 地址需要重新填写。`)) return;
  try {
    const r = await apiPost('/api/devices/' + id + '/copy');
    toast(`已复制为新设备「${r.name}」（含 ${r.pointCount} 个测点），请到测点管理填写 PLC 地址`);
    loadDevices();
  } catch (e) {
    toast(e.message, 'err');
  }
}

async function toggleDevice(id) {
  const d = devices.find(x => x.id === id);
  if (!d) return;
  try {
    await apiPut('/api/devices/' + id, {
      name: d.name, code: d.code, description: d.description,
      sortOrder: d.sortOrder, enabled: !d.enabled
    });
    toast(d.enabled ? '设备已禁用' : '设备已启用');
    loadDevices();
  } catch (e) {
    toast(e.message, 'err');
  }
}

async function deleteDevice(id) {
  const d = devices.find(x => x.id === id);
  if (!confirmAction(`确定删除设备「${d ? d.name : id}」吗？\n该设备下全部测点、相关报警与历史趋势数据将一并删除！`)) return;
  try {
    await apiDelete('/api/devices/' + id);
    toast('设备已删除');
    loadDevices();
  } catch (e) {
    toast(e.message, 'err');
  }
}

renderLayout('设备管理');
bindModalClose('device-modal');
document.getElementById('device-save-btn').addEventListener('click', saveDevice);
document.getElementById('device-form').addEventListener('keydown', (e) => {
  if (e.key === 'Enter') { e.preventDefault(); saveDevice(); }
});
loadDevices();

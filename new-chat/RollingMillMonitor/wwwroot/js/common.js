/* ============================================================
   公共脚本：API 封装 / Toast / 弹窗 / 格式化 / SignalR / 导航
   ============================================================ */

/* ---------- API 封装（统一返回格式 {success, message, field, data}） ---------- */
async function api(url, options = {}) {
  const opts = { ...options };
  opts.headers = { 'Content-Type': 'application/json', ...(opts.headers || {}) };
  const res = await fetch(url, opts);
  let body = null;
  try { body = await res.json(); } catch (e) { /* 非 JSON */ }
  if (!res.ok || (body && body.success === false)) {
    const err = new Error(body && body.message ? body.message : ('请求失败（HTTP ' + res.status + '）'));
    err.field = body ? body.field : null;
    err.data = body ? body.data : null;
    throw err;
  }
  return body ? body.data : null;
}

const apiGet  = (u) => api(u);
const apiPost = (u, d) => api(u, { method: 'POST', body: JSON.stringify(d) });
const apiPut  = (u, d) => api(u, { method: 'PUT', body: JSON.stringify(d) });
const apiDelete = (u) => api(u, { method: 'DELETE' });

/* ---------- Toast ---------- */
function toast(msg, type = 'ok', ms = 3500) {
  const box = document.getElementById('toast-box');
  if (!box) return;
  const el = document.createElement('div');
  el.className = 'toast ' + (type === 'err' ? 'err' : type === 'warn' ? 'warn' : 'ok');
  el.textContent = msg;
  box.appendChild(el);
  setTimeout(() => { el.style.opacity = '0'; el.style.transition = 'opacity .3s'; }, ms);
  setTimeout(() => el.remove(), ms + 350);
}

/* ---------- 弹窗 ---------- */
function openModal(id) {
  const el = document.getElementById(id);
  if (el) el.classList.add('show');
}
function closeModal(id) {
  const el = document.getElementById(id);
  if (el) el.classList.remove('show');
}
function bindModalClose(id) {
  const el = document.getElementById(id);
  if (!el) return;
  const closeBtn = el.querySelector('.modal-close');
  if (closeBtn) closeBtn.addEventListener('click', () => closeModal(id));
  el.addEventListener('click', (e) => { if (e.target === el) closeModal(id); });
}

/* ---------- 格式化 ---------- */
function fmtVal(v) {
  if (v === null || v === undefined || v === '') return '—';
  const n = Number(v);
  if (!isFinite(n)) return '—';
  const abs = Math.abs(n);
  if (abs >= 1000) return n.toFixed(0);
  if (abs >= 100) return n.toFixed(1);
  if (abs >= 10) return n.toFixed(2);
  return n.toFixed(3);
}
function fmtTime(t) {
  if (!t) return '—';
  const d = new Date(t);
  if (isNaN(d.getTime())) return '—';
  const p = (x) => String(x).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth()+1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}
function fmtDur(sec) {
  if (sec === null || sec === undefined) return '—';
  sec = Math.floor(sec);
  const h = Math.floor(sec / 3600), m = Math.floor((sec % 3600) / 60), s = sec % 60;
  if (h > 0) return `${h}小时${m}分`;
  if (m > 0) return `${m}分${s}秒`;
  return `${s}秒`;
}
function escapeHtml(s) {
  return String(s ?? '').replace(/[&<>"']/g, c => ({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;' }[c]));
}
/* 本地时间 ISO 字符串（不带时区 Z）：后端按本地时间存储/查询，传 UTC 会导致查询范围为 0 */
function toLocalIso(d) {
  const p = (x) => String(x).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth()+1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

/* 状态徽标 HTML */
function statusBadge(status, name) {
  const cls = 'st-' + (status === 'Waiting' ? 'waiting' : status.toLowerCase());
  return `<span class="badge ${cls}"><span class="dot"></span>${escapeHtml(name || status)}</span>`;
}

/* 报警级别徽标 */
function alarmLevelBadge(level) {
  const map = { Critical: '严重', Warning: '警告' };
  const cls = level === 'Critical' ? 'alert-lv-critical' : 'alert-lv-warning';
  return `<span class="badge ${cls}">${map[level] || level}</span>`;
}

/* 报警类型名 */
function alarmTypeName(t) {
  return ({ HighHigh: '高高报警', High: '高报警', Low: '低报警', LowLow: '低低报警', Comm: '通信报警', Fault: '故障报警' })[t] || t;
}

/* ---------- 开关量（Bool）状态测点 ---------- */
/* 现场油流/油压为开关量：1=正常，0=故障。页面显示"正常/故障"，不再显示数字模拟量。 */
function isBoolType(dataType) {
  return (dataType || '').toUpperCase() === 'BOOL';
}
function boolStateText(v) {
  if (v === null || v === undefined) return '—';
  return Number(v) === 1 ? '正常' : '故障';
}
/* 总览卡片里 Bool 测点的实时值 HTML（正常=绿色，故障=红色） */
function boolStateHtml(p) {
  if (p.value === null || p.value === undefined) return '<span class="muted">—</span>';
  const fault = Number(p.value) !== 1;
  const cls = fault ? 'big-val alert-lv-critical' : 'big-val ok-val';
  return `<span class="${cls}">${fault ? '故障' : '正常'}</span>`;
}
/* 通用测点值格式化：Bool 测点显示 正常/故障，其余走数字格式化 */
function fmtPointValue(p) {
  if (p.value === null || p.value === undefined) return '—';
  return isBoolType(p.dataType) ? boolStateText(p.value) : fmtVal(p.value);
}

/* ---------- 导航 / 顶栏 ---------- */
const NAV_ITEMS = [
  { href: 'index.html', icon: '▦', text: '总览' },
  { href: 'plcs.html', icon: '⛓', text: 'PLC 管理' },
  { href: 'devices.html', icon: '⚙', text: '设备管理' },
  { href: 'points.html', icon: '◎', text: '测点管理' },
  { href: 'alarms.html', icon: '⚠', text: '报警管理' },
  { href: 'trends.html', icon: '📈', text: '趋势' },
  { href: 'config.html', icon: '⇅', text: '导入导出' },
];

function renderLayout(pageTitle) {
  const sidebar = document.getElementById('sidebar');
  if (sidebar) {
    const current = location.pathname.split('/').pop() || 'index.html';
    sidebar.innerHTML = `
      <div class="brand"><div class="logo">⚙</div><span class="txt">轧机监控平台</span></div>
      <nav>${NAV_ITEMS.map(i =>
        `<a href="${i.href}" class="${i.href === current ? 'active' : ''}">
           <span class="nav-icon">${i.icon}</span><span class="txt">${i.text}</span>
         </a>`).join('')}
      </nav>
      <div class="foot"><span class="conn-dot" id="conn-dot"></span><span id="conn-text">连接中…</span></div>`;
  }
  const title = document.getElementById('page-title');
  if (title) title.textContent = pageTitle;
  startClock();
}

function startClock() {
  const el = document.getElementById('clock');
  if (!el) return;
  const tick = () => { el.textContent = new Date().toLocaleString('zh-CN', { hour12: false }); };
  tick();
  setInterval(tick, 1000);
}

function setConnStatus(ok) {
  const dot = document.getElementById('conn-dot');
  const txt = document.getElementById('conn-text');
  if (!dot || !txt) return;
  if (ok) { dot.className = 'conn-dot on'; txt.textContent = '实时连接正常'; }
  else { dot.className = 'conn-dot'; txt.textContent = '实时连接断开'; }
}

/* ---------- SignalR ---------- */
let connection = null;
let signalrConnected = false;
const _signalrStateCallbacks = [];

/** 注册连接状态变化回调（用于断线时前端轮询兜底） */
function onSignalrState(fn) { _signalrStateCallbacks.push(fn); }

function _setSignalrState(ok) {
  signalrConnected = ok;
  setConnStatus(ok);
  _signalrStateCallbacks.forEach(fn => { try { fn(ok); } catch (e) { } });
}

function connectSignalr(handlers) {
  if (typeof signalR === 'undefined') {
    _setSignalrState(false);
    return;
  }
  connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/monitor')
    .withAutomaticReconnect()
    .build();

  for (const [name, fn] of Object.entries(handlers || {})) {
    connection.on(name, fn);
  }

  connection.start()
    .then(() => _setSignalrState(true))
    .catch(() => _setSignalrState(false));

  connection.onreconnected(() => _setSignalrState(true));
  connection.onclose(() => _setSignalrState(false));
}

/* ---------- 表单错误显示 ---------- */
function showFieldErrors(formEl, err) {
  formEl.querySelectorAll('.field').forEach(f => f.classList.remove('err'));
  formEl.querySelectorAll('.f-err').forEach(e => e.remove());
  if (!err || !err.field) return;
  const field = formEl.querySelector(`[data-field="${err.field}"]`);
  if (field) {
    field.classList.add('err');
    const fe = document.createElement('div');
    fe.className = 'f-err';
    fe.textContent = err.message;
    field.appendChild(fe);
  }
}

/* ---------- 通用确认 ---------- */
function confirmAction(msg) {
  return window.confirm(msg);
}

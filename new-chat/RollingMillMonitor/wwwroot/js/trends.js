/* 趋势页面：ECharts 多测点对比 + 数据表格 + CSV 导出 */

let chart = null;
let allPoints = [];
let selectedIds = [];
let rangeSec = 3600;
let lastData = null;

const PALETTE = ['#2f81f7', '#2ea043', '#d29922', '#f0883e', '#c777e9', '#f85149', '#58a6ff', '#56d4dd', '#e3b341', '#a371f7'];

async function loadPoints() {
  try {
    allPoints = await apiGet('/api/points');
    const box = document.getElementById('point-picker');
    if (!allPoints.length) {
      box.innerHTML = '<div class="empty">暂无测点，请先到测点管理新增</div>';
      return;
    }
    const byDevice = {};
    allPoints.forEach(p => {
      (byDevice[p.deviceName] = byDevice[p.deviceName] || []).push(p);
    });
    box.innerHTML = Object.entries(byDevice).map(([dev, pts]) => `
      <div class="mb-12">
        <div class="muted small" style="margin-bottom:6px">${escapeHtml(dev)}</div>
        ${pts.map(p =>
          `<label class="small" style="margin-right:14px;cursor:pointer">
             <input type="checkbox" class="pt-check" value="${p.id}" ${selectedIds.includes(p.id) ? 'checked' : ''}>
             ${escapeHtml(p.name)}（${escapeHtml(p.unit) || '无单位'}）
           </label>`).join('')}
      </div>`).join('');
    box.querySelectorAll('.pt-check').forEach(cb => cb.addEventListener('change', () => {
      selectedIds = [...box.querySelectorAll('.pt-check:checked')].map(c => parseInt(c.value));
      loadTrend();
    }));
  } catch (e) {
    toast(e.message, 'err');
  }
}

function setRange(sec) {
  rangeSec = sec;
  document.querySelectorAll('[data-range]').forEach(b =>
    b.classList.toggle('primary', parseInt(b.dataset.range) === sec));
  loadTrend();
}

async function loadTrend() {
  if (!selectedIds.length) {
    const box = document.getElementById('trend-chart');
    box.innerHTML = '<div class="empty" style="padding-top:120px">请勾选至少一个测点</div>';
    return;
  }
  try {
    const to = new Date();
    const from = new Date(to.getTime() - rangeSec * 1000);
    const qs = new URLSearchParams();
    qs.set('pointIds', selectedIds.join(','));
    qs.set('from', toLocalIso(from));
    qs.set('to', toLocalIso(to));
    qs.set('maxRows', '8000');
    const data = await apiGet('/api/trends?' + qs.toString());
    lastData = data;
    renderChart(data);
    renderTable(data);
  } catch (e) {
    toast(e.message, 'err');
  }
}

function renderChart(data) {
  const el = document.getElementById('trend-chart');
  if (!chart) {
    chart = echarts.init(el, null, { renderer: 'canvas' });
    window.addEventListener('resize', () => chart && chart.resize());
  }
  const series = data.series.map((s, i) => ({
    name: s.name + '（' + (s.unit || '—') + '）',
    type: 'line',
    showSymbol: false,
    smooth: false,
    connectNulls: false,
    lineStyle: { width: 1.6 },
    itemStyle: { color: PALETTE[i % PALETTE.length] },
    data: s.data.map(d => [d.t, d.v])
  }));
  chart.setOption({
    backgroundColor: 'transparent',
    color: PALETTE,
    tooltip: {
      trigger: 'axis',
      backgroundColor: '#1b2430',
      borderColor: '#2b3648',
      textStyle: { color: '#d7dee7', fontSize: 12 },
      valueFormatter: (v) => (v === null || v === undefined ? '—' : fmtVal(v))
    },
    legend: { textStyle: { color: '#8b98a9' }, top: 0, type: 'scroll' },
    grid: { left: 50, right: 24, top: 40, bottom: 70 },
    xAxis: {
      type: 'time',
      axisLine: { lineStyle: { color: '#2b3648' } },
      axisLabel: { color: '#8b98a9' },
      splitLine: { lineStyle: { color: '#1e2936' } }
    },
    yAxis: {
      type: 'value',
      scale: true,
      axisLabel: { color: '#8b98a9' },
      splitLine: { lineStyle: { color: '#1e2936' } }
    },
    dataZoom: [
      { type: 'inside', start: 0, end: 100 },
      { type: 'slider', height: 18, bottom: 14, borderColor: '#2b3648', backgroundColor: '#161b22', fillerColor: 'rgba(47,129,247,.15)', handleStyle: { color: '#2f81f7' }, textStyle: { color: '#8b98a9' } }
    ],
    series
  });
}

function renderTable(data) {
  const tb = document.getElementById('trend-tbody');
  document.getElementById('table-info').textContent =
    `共 ${data.rows.length} 条记录（${fmtTime(data.from)} ~ ${fmtTime(data.to)}）`;
  if (!data.rows.length) {
    tb.innerHTML = '<tr><td colspan="5" class="empty">该时间段暂无数据（采集服务正在积累历史数据）</td></tr>';
    return;
  }
  tb.innerHTML = data.rows.map(r => `
    <tr>
      <td class="small muted">${fmtTime(r.timestamp)}</td>
      <td>${escapeHtml(r.deviceName)}</td>
      <td>${escapeHtml(r.pointName)}</td>
      <td class="num">${r.value === null || r.value === undefined ? '<span class="muted">—</span>' : fmtVal(r.value)}</td>
      <td class="small">${escapeHtml(r.status)}</td>
    </tr>`).join('');
}

function exportCsv() {
  if (!selectedIds.length) { toast('请先勾选测点', 'warn'); return; }
  const to = new Date();
  const from = new Date(to.getTime() - rangeSec * 1000);
  const qs = new URLSearchParams();
  qs.set('pointIds', selectedIds.join(','));
  qs.set('from', toLocalIso(from));
  qs.set('to', toLocalIso(to));
  window.open('/api/trends/export?' + qs.toString(), '_blank');
}

renderLayout('历史趋势');
document.querySelectorAll('[data-range]')[0].classList.add('primary');
loadPoints();

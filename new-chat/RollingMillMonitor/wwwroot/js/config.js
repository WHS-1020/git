/* 配置导入导出页面 */

function exportJson() {
  window.open('/api/config/export?format=json', '_blank');
}

function exportCsv() {
  window.open('/api/config/export?format=csv', '_blank');
}

function handleFile(file) {
  document.getElementById('file-name').textContent = file.name + '（' + (file.size / 1024).toFixed(1) + ' KB）';
  const reader = new FileReader();
  reader.onload = async () => {
    const format = file.name.toLowerCase().endsWith('.csv') ? 'csv' : 'json';
    await doImport(format, reader.result);
  };
  reader.onerror = () => toast('文件读取失败', 'err');
  reader.readAsText(file, 'utf-8');
}

async function doImport(format, content) {
  const box = document.getElementById('import-result');
  box.innerHTML = '<div class="muted small">导入校验中…</div>';
  try {
    const r = await apiPost('/api/config/import', { format, content });
    box.innerHTML = `<div class="ok-box">
      导入成功：PLC ${r.plcCount} 个、设备 ${r.deviceCount} 个、测点 ${r.pointCount} 个、报警阈值 ${r.thresholdCount} 个。
    </div>`;
    toast('配置导入成功');
  } catch (e) {
    const errors = (e.data && e.data.errors) || [];
    box.innerHTML = `<div class="err-box">
      <div><b>${escapeHtml(e.message)}</b></div>
      ${errors.slice(0, 200).map(x =>
        `<div class="err-line">第 ${x.line || '?'} 行：${escapeHtml(x.message)}</div>`).join('') || '<div class="err-line">无详细错误信息</div>'}
    </div>`;
    toast('导入失败', 'err');
  }
}

renderLayout('配置导入导出');

const fileInput = document.getElementById('import-file');
fileInput.addEventListener('change', () => {
  if (fileInput.files.length) handleFile(fileInput.files[0]);
});

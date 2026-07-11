const fallbackData = {
  meta: { mode: "保守模式", source: "未加载真实数据", rules: 0, lastScan: "--:--" },
  drive: { name: "C:", total: "0 B", used: "0 B", free: "0 B", usedPercent: 0 },
  metrics: [
    { label: "系统盘已用", value: "0 B", hint: "未加载真实数据" },
    { label: "预计可安全清理", value: "0 B", hint: "等待扫描引擎生成 data.json" },
    { label: "建议迁移空间", value: "0 B", hint: "等待扫描引擎生成 data.json" },
    { label: "需官方处理", value: "0 B", hint: "等待扫描引擎生成 data.json" }
  ],
  usageBars: [],
  recommendations: [],
  diagnosis: [],
  cleanup: [],
  migration: [],
  official: [],
  quarantine: [],
  reports: [],
  rules: [],
  settings: {},
  issues: []
};

const views = {
  overview: ["总览", "以空间治理为目标，区分可清理、可迁移、官方清理、专项处理与禁止操作。"],
  diagnosis: ["空间诊断", "找到真正的大户，并给出清理、迁移、官方清理或保留建议。"],
  cleanup: ["安全清理", "只处理低风险、可回滚项目，避免把深度扫描变成深度误删。"],
  migration: ["迁移建议", "释放大空间优先靠迁移用户数据和软件数据，而不是直接删除。"],
  official: ["官方清理", "Windows 组件、更新缓存和系统文件必须通过官方方式处理。"],
  quarantine: ["隔离区", "清理项目先隔离，可恢复、可追踪、可校验。"],
  rules: ["规则库", "用本地规则扩展软件识别和处理建议，减少硬编码。"],
  settings: ["设置", "控制扫描范围、隔离保留、日志、自检和高级显示。"],
  trust: ["可信中心", "查看隐私边界、异常排查、安装部署和版本可信信息。"]
};

const navIcons = {
  overview: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M4 13h7V4H4v9Zm9 7h7V4h-7v16ZM4 20h7v-5H4v5Z"/></svg>',
  diagnosis: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M4 19V5m0 14h16M8 16V9m4 7V7m4 9v-4"/></svg>',
  cleanup: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M4 7h16M9 7V5h6v2m-8 0 1 13h8l1-13M10 11v5m4-5v5"/></svg>',
  migration: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M7 7h10v10H7zM3 12h4m10 0h4m-9-9v4m0 10v4"/></svg>',
  official: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M12 3 4 6v6c0 5 3.5 8 8 9 4.5-1 8-4 8-9V6l-8-3Z"/><path d="m9 12 2 2 4-5"/></svg>',
  quarantine: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M4 7h16v13H4zM8 7V4h8v3M9 12h6"/></svg>',
  rules: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M4 5h16M4 12h16M4 19h16"/><path d="M8 3v4m8 3v4m-5 3v4"/></svg>',
  settings: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Z"/><path d="M4 12h2m12 0h2M12 4v2m0 12v2M6.3 6.3l1.4 1.4m8.6 8.6 1.4 1.4m0-11.4-1.4 1.4m-8.6 8.6-1.4 1.4"/></svg>',
  trust: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M12 3 5 6v5c0 4.5 2.8 8 7 10 4.2-2 7-5.5 7-10V6l-7-3Z"/><path d="M9 12l2 2 4-5"/><path d="M8 18h8"/></svg>'
};

const tagClass = text => {
  if ((text || "").includes("清理")) return "clean";
  if ((text || "").includes("迁移")) return "move";
  if ((text || "").includes("官方")) return "official";
  if ((text || "").includes("专项")) return "special";
  if ((text || "").includes("禁止")) return "block";
  return "keep";
};

const actionSummary = [
  { key: "safeClean", label: "可清理", tone: "clean" },
  { key: "migration", label: "可迁移", tone: "move" },
  { key: "official", label: "官方清理", tone: "official" },
  { key: "blocked", label: "禁止操作", tone: "block" }
];

let nativeBusy = false;

const defaultSettings = {
  defaultMode: "Quick",
  autoScan: true,
  quarantineDays: 7,
  maxFiles: 3000,
  browserCache: true,
  developerCache: true,
  logging: true,
  advancedEvidence: true
};

function loadLocalSettings(dataSettings = {}) {
  try {
    const saved = JSON.parse(localStorage.getItem("mylSettings") || "{}");
    return { ...defaultSettings, ...dataSettings, ...saved };
  } catch {
    return { ...defaultSettings, ...dataSettings };
  }
}

function saveLocalSettings(settings) {
  localStorage.setItem("mylSettings", JSON.stringify(settings));
  sendNative("saveSettings", settings);
}

const escapeHtml = value => String(value ?? "")
  .replaceAll("&", "&amp;")
  .replaceAll("<", "&lt;")
  .replaceAll(">", "&gt;")
  .replaceAll('"', "&quot;");

const parseSize = value => {
  const match = String(value ?? "").replaceAll(",", "").match(/([\d.]+)\s*(B|KB|MB|GB|TB)/i);
  if (!match) return 0;
  const units = { B: 1, KB: 1024, MB: 1024 ** 2, GB: 1024 ** 3, TB: 1024 ** 4 };
  return Number(match[1]) * (units[match[2].toUpperCase()] || 1);
};

const formatBytes = bytes => {
  if (bytes >= 1024 ** 4) return `${(bytes / 1024 ** 4).toFixed(2)} TB`;
  if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(2)} GB`;
  if (bytes >= 1024 ** 2) return `${(bytes / 1024 ** 2).toFixed(2)} MB`;
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(2)} KB`;
  return `${Math.round(bytes)} B`;
};

const cleanupValue = (row, key) => {
  if (key === "size") return parseSize(row.size);
  return String(row[key] ?? "").toLowerCase();
};

const rowValue = (row, key) => {
  if (key === "size") return parseSize(row.size);
  if (key === "count") return Number(row.count || 0);
  return String(row[key] ?? "").toLowerCase();
};

async function loadData() {
  try {
    const res = await fetch("./data.json", { cache: "no-store" });
    if (!res.ok) throw new Error("data fetch failed");
    return await res.json();
  } catch {
    return fallbackData;
  }
}

function renderNav() {
  const nav = document.getElementById("nav");
  nav.innerHTML = Object.entries(views).map(([key, value], index) => `
    <button class="${index === 0 ? "active" : ""}" data-view="${key}">
      ${navIcons[key]}${value[0]}
    </button>
  `).join("");

  nav.querySelectorAll("button").forEach(btn => {
    btn.addEventListener("click", () => {
      document.querySelectorAll(".nav button").forEach(b => b.classList.remove("active"));
      document.querySelectorAll(".view").forEach(v => v.classList.remove("active"));
      btn.classList.add("active");
      document.getElementById(`${btn.dataset.view}View`).classList.add("active");
      document.getElementById("pageTitle").textContent = views[btn.dataset.view][0];
      document.getElementById("pageDesc").textContent = views[btn.dataset.view][1];
    });
  });
}

function renderStatus(meta) {
  document.getElementById("statusPills").innerHTML = `
    <span class="pill green">数据：${escapeHtml(meta.source || "演示")}</span>
    <span class="pill blue">规则：${escapeHtml(meta.rules)} 条</span>
    <span class="pill amber">扫描：${escapeHtml(meta.lastScan)}</span>
    <span class="pill ${meta.admin ? "green" : "amber"}">${escapeHtml(meta.adminStatus || "权限未知")}</span>
    ${meta.admin ? "" : `<button class="btn mini" data-restart-admin>管理员重启</button>`}
  `;
  const restart = document.querySelector("[data-restart-admin]");
  if (restart) restart.addEventListener("click", () => sendNative("restartAdmin"));
}

function showToast(message) {
  const toast = document.getElementById("toast");
  toast.textContent = message;
  toast.classList.add("show");
  clearTimeout(showToast.timer);
  showToast.timer = setTimeout(() => toast.classList.remove("show"), 2200);
}

function setButtonBusy(button, text) {
  if (!button) return;
  button.dataset.originalText = button.dataset.originalText || button.textContent;
  button.textContent = text;
  button.disabled = true;
  button.classList.add("loading");
}

function openDrawer(title, subtitle, fields) {
  document.getElementById("drawerTitle").textContent = title;
  document.getElementById("drawerSubtitle").textContent = subtitle;
  document.getElementById("drawerBody").innerHTML = `
    <div class="detail-list">
      ${fields.map(item => `<div><b>${escapeHtml(item.label)}</b>${escapeHtml(item.value)}</div>`).join("")}
    </div>
  `;
  document.getElementById("drawer").classList.add("open");
  document.getElementById("drawer").setAttribute("aria-hidden", "false");
}

function closeDrawer() {
  document.getElementById("drawer").classList.remove("open");
  document.getElementById("drawer").setAttribute("aria-hidden", "true");
}

function openFirstRunGuide(meta) {
  document.getElementById("drawerTitle").textContent = "首次使用引导";
  document.getElementById("drawerSubtitle").textContent = "先理解边界，再开始清理";
  document.getElementById("drawerBody").innerHTML = `
    <div class="guide-list">
      <div><b>1. 普通模式也能用</b><span>当前状态：${escapeHtml(meta.adminStatus || "权限未知")}。普通模式会跳过部分系统目录，管理员模式扫描更完整。</span></div>
      <div><b>2. 默认只分析，不自动删除</b><span>诊断、迁移、官方处理和禁止项都只是建议；只有安全清理页勾选并二次确认后才会执行。</span></div>
      <div><b>3. 深度诊断不等于深度删除</b><span>深度结果会分成迁移、官方清理、专项清理、禁止操作，不会一键删除系统或用户数据。</span></div>
      <div><b>4. 清理先进入隔离区</b><span>低风险候选执行前会二次确认，执行后先隔离，可按批次恢复，并生成报告。</span></div>
      <div><b>5. 用户数据优先迁移</b><span>下载、素材、聊天文件和软件数据会给出源路径、目标路径和观察期建议，不直接替你删除。</span></div>
      <div><b>6. 隐私边界</b><span>工具读取路径、大小、时间、签名和归属线索，不展示或上传文件内容。</span></div>
      <div><b>7. 报告可追溯</b><span>每次清理/恢复都会记录成功、失败和跳过原因；可信中心里也有异常排查。</span></div>
      <div class="confirm-actions"><button class="btn primary" data-guide-done>知道了</button></div>
    </div>
  `;
  document.getElementById("drawer").classList.add("open");
  document.getElementById("drawer").setAttribute("aria-hidden", "false");
  document.querySelector("[data-guide-done]").addEventListener("click", () => {
    localStorage.setItem("mylGuideSeen", "1");
    closeDrawer();
  });
}

function openCleanConfirm(items) {
  if (!items.length) {
    showToast("请先选择要清理的候选项");
    return;
  }
  const totalBytes = items.reduce((sum, item) => sum + parseSize(item.size), 0);
  const totalText = formatBytes(totalBytes);
  document.getElementById("drawerTitle").textContent = "清理前确认";
  document.getElementById("drawerSubtitle").textContent = `本次将隔离 ${items.length} 项，预计释放 ${totalText}`;
  document.getElementById("drawerBody").innerHTML = `
      <div class="confirm-box">
      <div class="safety-rail">
        <div><b>清理前快照</b><span>记录原路径、隔离路径、批次号、大小和执行时间。</span></div>
        <div><b>占用复核</b><span>桌面端执行前会跳过正在使用或受保护的对象。</span></div>
        <div><b>可恢复</b><span>文件先移动到隔离区，后续可按批次恢复到原路径。</span></div>
      </div>
      <div class="confirm-warning">
        <strong>不会直接永久删除。</strong>
        <span>执行后文件会先移动到隔离区，记录原路径、大小、时间和快照信息，可在隔离区恢复。</span>
      </div>
      <div class="confirm-list">
        ${items.map(item => `
          <div class="confirm-item">
            <div><b>${escapeHtml(item.name)}</b><span>${escapeHtml(item.owner || "未识别软件")} · ${escapeHtml(item.path || "未提供路径")}</span></div>
            <em>${escapeHtml(item.size)}</em>
          </div>
        `).join("")}
      </div>
      <label class="confirm-check"><input type="checkbox" data-confirm-clean /> 我已确认这些都是低风险候选，并同意先移动到隔离区；如果结果不符合预期，可到“隔离区”恢复。</label>
      <div class="operation-progress" data-operation-progress="clean">
        <div class="progress-meta"><span data-operation-title>等待执行</span><span data-operation-count>0 / ${items.length}</span></div>
        <div class="progress"><i data-operation-bar></i></div>
        <div class="operation-stats">
          <span data-operation-ok>成功 0</span>
          <span data-operation-skip>跳过 0</span>
          <span data-operation-fail>失败 0</span>
        </div>
        <p data-operation-file>确认后开始隔离。</p>
      </div>
      <div class="confirm-actions">
        <button class="btn" data-cancel-clean>取消</button>
        <button class="btn danger" data-execute-clean disabled>移动到隔离区</button>
      </div>
    </div>
  `;
  const drawer = document.getElementById("drawer");
  drawer.classList.add("open");
  drawer.setAttribute("aria-hidden", "false");
  const check = drawer.querySelector("[data-confirm-clean]");
  const execute = drawer.querySelector("[data-execute-clean]");
  check.addEventListener("change", () => {
    execute.disabled = !check.checked;
  });
  drawer.querySelector("[data-cancel-clean]").addEventListener("click", closeDrawer);
  execute.addEventListener("click", () => {
    if (nativeBusy) {
      showToast("任务正在执行，请等待当前操作完成");
      return;
    }
    nativeBusy = true;
    setButtonBusy(execute, `正在隔离 ${items.length} 项...`);
    check.disabled = true;
    drawer.querySelector("[data-cancel-clean]").disabled = true;
    showToast("正在隔离文件并写入快照，不会重新扫描整盘");
    const sent = sendNative("executeClean", {
      items: items.map(item => ({
        id: item.id,
        name: item.name,
        path: item.path,
        size: item.size,
        source: item.source,
        owner: item.owner,
        reason: item.reason
      }))
    });
    if (!sent) {
      nativeBusy = false;
      execute.disabled = false;
      execute.textContent = execute.dataset.originalText || "移动到隔离区";
      check.disabled = false;
      drawer.querySelector("[data-cancel-clean]").disabled = false;
    }
  });
}

function simulateProgress(scope, label = "正在扫描") {
  const wrap = document.querySelector(scope);
  if (!wrap) return;
  const bar = wrap.querySelector(".progress i");
  const text = wrap.querySelector("[data-progress-text]");
  const modules = [
    "数字签名校验已启用",
    "注册表软件归属识别已启用",
    "Windows 组件数据库识别已启用",
    "软件正在使用关系分析已启用",
    "文件信誉库比对已启用",
    "清理前快照策略已启用"
  ];
  const paths = [
    "C:\\Windows\\SoftwareDistribution\\Download\\{id}.tmp",
    "C:\\Users\\*\\AppData\\Local\\Temp\\myl_scan_{id}.cache",
    "C:\\ProgramData\\Package Cache\\{{id}}\\payload.bin",
    "C:\\Windows\\WinSxS\\Manifests\\amd64_policy_{id}.manifest",
    "HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{{id}}",
    "C:\\Users\\*\\AppData\\Local\\Microsoft\\Windows\\INetCache\\{id}.dat"
  ];
  const makeId = () => Math.random().toString(16).slice(2, 10).toUpperCase();
  wrap.classList.add("active");
  let value = 0;
  let tick = 0;
  const timer = setInterval(() => {
    tick += 1;
    value += Math.ceil(Math.random() * 9);
    if (value >= 100) {
      value = 100;
      clearInterval(timer);
      showToast("任务完成，已生成最新结果");
      setTimeout(() => wrap.classList.remove("active"), 700);
    }
    bar.style.width = `${value}%`;
    const module = modules[tick % modules.length];
    const path = paths[Math.floor(Math.random() * paths.length)].replace("{id}", makeId());
    text.textContent = `${label} ${value}% · ${module} · 正在分析 ${path}`;
  }, 120);
}

function sendNative(action, payload = {}) {
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage({ action, payload });
    return true;
  }
  showToast("请在桌面版中使用此功能");
  return false;
}

function riskText(item) {
  if (item.riskExplanation) return item.riskExplanation;
  if (item.action === "专项清理") return "需要使用对应软件命令处理，工具不会直接删除。";
  if (item.action === "官方清理") return "涉及系统组件，只能通过 Windows 官方工具处理。";
  if (item.action === "迁移") return "这是用户或软件数据，只建议迁移，不自动删除。";
  if (item.action === "禁止") return "该路径受保护或影响系统，不提供清理入口。";
  return "低风险项仍会在执行前二次确认并进入隔离区。";
}

function buildEvidence(item = {}) {
  const path = item.path || "";
  const source = item.source || item.area || "未知来源";
  const owner = item.owner || "未识别软件";
  const publisher = item.publisher || "未获取";
  const signature = item.signature || "未校验";
  const action = item.action || "安全清理";
  const rules = [];
  if (/Windows\\(System32|WinSxS|servicing|Boot)/i.test(path)) {
    rules.push("命中 Windows 保护路径，只能给出官方处理建议");
  }
  if (/AppData\\Local\\Temp|INetCache|CrashDumps|SoftwareDistribution\\Download/i.test(path)) {
    rules.push("命中低风险临时/缓存/更新下载目录");
  }
  if (/Program Files|Program Files \(x86\)/i.test(path)) {
    rules.push("位于软件安装目录，默认不直接删除");
  }
  if (!rules.length) rules.push("按扫描规则、来源、风险等级和路径特征综合判断");
  return [
    `识别来源：${source}`,
    `软件归属：${owner}`,
    `发布者/签名：${publisher} / ${signature}`,
    `建议动作：${action}`,
    `规则依据：${rules.join("；")}`
  ].join("\n");
}

function handlingBoundary(item = {}) {
  const action = item.action || "";
  if (action.includes("官方")) return "工具不会直接处理系统组件、WinSxS、休眠文件等对象，只提供 Windows 官方入口或命令说明。";
  if (action.includes("迁移")) return "工具只给迁移路径和步骤，不直接删除原目录；建议保留 7 天观察期。";
  if (action.includes("禁止")) return "该项不提供清理按钮，避免影响系统启动、软件运行或用户数据。";
  return "执行清理时只移动到隔离区，不做永久删除；失败、跳过和原路径都会写入报告。";
}

function nextStepText(item = {}) {
  const action = item.action || "";
  if (action.includes("官方")) return "打开“官方清理”页，使用 Windows 存储设置、cleanmgr 或 DISM。";
  if (action.includes("迁移")) return "打开“迁移建议”页，按源路径、目标路径、校验、回滚期逐步处理。";
  if (action.includes("专项")) return "使用对应软件自带清理入口或专项命令，不建议手动删目录。";
  if (action.includes("禁止")) return "保持现状；如你确定它不是系统/软件关键文件，可先加入白名单避免反复提示。";
  return "确认详情无误后，在“安全清理”页勾选并移动到隔离区。";
}

window.addEventListener("message", event => {
  const msg = event.data || {};
  if (msg.type === "toast") showToast(msg.message || "操作完成");
  if (msg.type === "operationProgress") updateOperationProgress(msg);
  if (msg.type === "operationComplete") rememberOperationResult(msg);
  if (msg.type === "reload") {
    nativeBusy = false;
    window.location.reload();
  }
});

function updateOperationProgress(msg = {}) {
  const panel = document.querySelector(`[data-operation-progress="${msg.operation || "clean"}"]`) || document.querySelector("[data-operation-progress]");
  if (!panel) return;
  panel.classList.add("active");
  const total = Number(msg.total || 0);
  const done = Number(msg.done || 0);
  const percent = total ? Math.min(100, Math.round(done / total * 100)) : 0;
  const set = (selector, text) => {
    const node = panel.querySelector(selector);
    if (node) node.textContent = text;
  };
  set("[data-operation-title]", `${msg.stage || "处理中"} ${percent}%`);
  set("[data-operation-count]", `${done} / ${total}`);
  set("[data-operation-ok]", `成功 ${msg.moved ?? 0}`);
  set("[data-operation-skip]", `跳过 ${msg.stale ?? 0}`);
  set("[data-operation-fail]", `失败 ${msg.failed ?? 0}`);
  set("[data-operation-file]", msg.path || "正在写入处理记录...");
  const bar = panel.querySelector("[data-operation-bar]");
  if (bar) bar.style.width = `${percent}%`;
}

function rememberOperationResult(msg = {}) {
  localStorage.setItem("mylLastOperation", JSON.stringify({
    title: msg.title || "操作完成",
    moved: msg.moved || 0,
    stale: msg.stale || 0,
    failed: msg.failed || 0,
    bytes: msg.bytes || "0 B",
    report: msg.report || "",
    time: new Date().toLocaleString()
  }));
}

function metricCard(item) {
  return `<div class="card metric"><div class="label">${escapeHtml(item.label)}</div><div class="value">${escapeHtml(item.value)}</div><div class="hint">${escapeHtml(item.hint)}</div></div>`;
}

function queueItem(item) {
  return `
    <div class="queue-item">
      <div class="queue-title"><span>${escapeHtml(item.title)}</span><span class="tag ${tagClass(item.action)}">${escapeHtml(item.action || item.size)}</span></div>
      <p>${escapeHtml(item.detail)}</p>
    </div>
  `;
}

function emptyRow(colspan, text) {
  return `<tr><td colspan="${colspan}">${escapeHtml(text)}</td></tr>`;
}

function emptyPanel(text) {
  return `<div class="queue-item"><p>${escapeHtml(text)}</p></div>`;
}

function renderOverview(data) {
  const actionValues = {
    safeClean: data.metrics?.[1]?.value || "0 B",
    migration: data.metrics?.[2]?.value || "0 B",
    official: data.metrics?.[3]?.value || "0 B",
    blocked: (data.usageBars || []).find(row => row.name === "禁止手动处理")?.size || "0 B"
  };
  document.getElementById("overviewView").innerHTML = `
    <div class="source-banner">
      <div>数据来源：${escapeHtml(data.meta?.source || "未知")} · ${escapeHtml(data.drive?.name || "C:")} 总容量 ${escapeHtml(data.drive?.total || "0 B")} · 已用 ${escapeHtml(data.drive?.used || "0 B")}</div>
      <span>${escapeHtml(data.meta?.mode || "快速扫描")} · ${escapeHtml(data.meta?.adminStatus || "权限未知")} · 跳过目录 ${escapeHtml(data.meta?.skippedDirectories ?? 0)}</span>
    </div>
    <div class="grid cols-4">${data.metrics.map(metricCard).join("")}</div>
    <div class="mt grid cols-2">
      <div class="card">
        <div class="panel-head"><div><h2>C 盘健康概览</h2><p>展示空间结构，不直接删除文件。</p></div><button class="btn primary">开始诊断</button></div>
        <div class="panel-body drive">
          <div class="ring" style="background: conic-gradient(var(--blue) 0 ${data.drive.usedPercent}%, #dce6f2 ${data.drive.usedPercent}% 100%)"><span>${data.drive.usedPercent}%<small>已使用</small></span></div>
          <div class="bar-list">
            ${data.usageBars.map(row => `
              <div class="bar-row">
                <div class="bar-meta"><span>${escapeHtml(row.name)}</span><span>${escapeHtml(row.size)}</span></div>
                <div class="bar ${escapeHtml(row.tone)}"><i style="width:${Number(row.percent) || 0}%"></i></div>
              </div>
            `).join("")}
          </div>
        </div>
      </div>
      <div class="card">
        <div class="panel-head"><div><h2>推荐处理队列</h2><p>按风险和收益排序。</p></div></div>
        <div class="panel-body queue">${data.recommendations.length ? data.recommendations.map(queueItem).join("") : emptyPanel("暂无推荐项，请先运行桌面版扫描引擎。")}</div>
      </div>
    </div>
    <div class="mt card">
      <div class="panel-head"><div><h2>处理方式分布</h2><p>把深度扫描结果拆成不同处理策略，避免误删。</p></div></div>
      <div class="panel-body action-grid">
        ${actionSummary.map(item => `
          <div class="action-tile">
            <span>${escapeHtml(item.label)}</span>
            <strong>${escapeHtml(actionValues[item.key])}</strong>
            <div class="tag ${item.tone}">${escapeHtml(item.label)}</div>
          </div>
        `).join("")}
      </div>
    </div>
  `;
  document.querySelector("#overviewView .btn.primary").addEventListener("click", () => {
    document.querySelector('[data-view="diagnosis"]').click();
    showToast("已切换到空间诊断");
  });
}

function renderDiagnosis(rows, meta = {}) {
  const currentMode = meta.modeKey || (meta.mode === "深度诊断" ? "Deep" : meta.mode === "软件残留扫描" ? "SoftwareLeftover" : "Quick");
  document.getElementById("diagnosisView").innerHTML = `
    <div class="card">
      <div class="panel-head">
        <div><h2>空间大户排行</h2><p>当前模式：${escapeHtml(meta.mode || "快速扫描")}。切换模式后会重新生成数据。</p></div>
        <div class="toolbar">
          <select class="mode-select" data-scan-mode>
            <option value="Quick" ${currentMode === "Quick" ? "selected" : ""}>快速扫描</option>
            <option value="SoftwareLeftover" ${currentMode === "SoftwareLeftover" ? "selected" : ""}>软件残留扫描</option>
            <option value="Deep" ${currentMode === "Deep" ? "selected" : ""}>深度诊断</option>
          </select>
          <button class="btn primary" data-run-diagnosis>重新诊断</button>
          <button class="btn" data-export>导出 CSV</button>
        </div>
      </div>
      <div class="scan-levels">
        <div><b>快速扫描</b><span>只看临时目录、浏览器缓存、崩溃转储。</span></div>
        <div><b>软件残留扫描</b><span>扩展到 AppData、ProgramData，默认只复核不删除。</span></div>
        <div><b>深度诊断</b><span>扫描用户与软件大目录，输出迁移/官方/禁止建议。</span></div>
      </div>
      <div class="progress-wrap" id="diagnosisProgress"><div class="progress-meta"><span data-progress-text>准备诊断</span><span>只分析，不删除，可能需要 1-2 分钟</span></div><div class="progress"><i></i></div></div>
      <table>
        <thead><tr><th>区域</th><th>大小</th><th>建议动作</th><th>风险</th><th>路径</th><th>操作</th></tr></thead>
        <tbody>${rows.length ? rows.map((row, index) => `
          <tr data-row-type="diagnosis" data-index="${index}">
            <td><strong>${escapeHtml(row.area)}</strong></td>
            <td>${escapeHtml(row.size)}</td>
            <td><span class="tag ${tagClass(row.action)}">${escapeHtml(row.action)}</span></td>
            <td>${escapeHtml(row.risk)}</td>
            <td>${escapeHtml(row.path)}</td>
            <td>${row.action === "专项清理" ? `<button class="btn mini" data-special-index="${index}">专项说明</button>` : row.action === "官方清理" ? `<button class="btn mini" data-official-hint="${index}">官方入口</button>` : ""}</td>
          </tr>
        `).join("") : emptyRow(6, "暂无诊断数据，请先运行桌面版扫描引擎。")}</tbody>
      </table>
    </div>
  `;
  document.querySelector("[data-run-diagnosis]").addEventListener("click", event => {
    if (nativeBusy) {
      showToast("任务正在执行，请等待当前操作完成");
      return;
    }
    nativeBusy = true;
    setButtonBusy(event.currentTarget, "诊断中...");
    simulateProgress("#diagnosisProgress", "正在诊断");
    sendNative("refreshData", { mode: document.querySelector("[data-scan-mode]").value });
  });
  document.querySelector("[data-export]").addEventListener("click", () => sendNative("exportReport"));
  document.querySelectorAll('[data-row-type="diagnosis"]').forEach(row => {
    row.addEventListener("click", () => {
      const item = rows[Number(row.dataset.index)];
      openDrawer(item.area, "空间诊断详情", [
        { label: "占用空间", value: item.size },
        { label: "建议动作", value: item.action },
        { label: "风险等级", value: item.risk },
        { label: "路径", value: item.path },
        { label: "建议", value: item.recommendation || `建议按“${item.action}”方式处理。` },
        { label: "原因", value: item.reason || "-" },
        { label: "判断依据", value: buildEvidence(item) },
        { label: "风险解释", value: riskText(item) },
        { label: "处理边界", value: handlingBoundary(item) },
        { label: "下一步", value: nextStepText(item) },
        { label: "专项命令", value: item.specialCommand || "无" }
      ]);
    });
  });
  document.querySelectorAll("[data-special-index]").forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      const item = rows[Number(button.dataset.specialIndex)];
      sendNative("specialAction", { key: item.specialKey || "special", command: item.specialCommand || "使用对应软件的缓存清理入口" });
    });
  });
  document.querySelectorAll("[data-official-hint]").forEach(button => {
    button.addEventListener("click", event => {
      event.stopPropagation();
      document.querySelector('[data-view="official"]').click();
      showToast("已切换到官方清理入口");
    });
  });
}

function renderCleanup(rows) {
  const state = { filterKey: "name", filterText: "", sortKey: "size", sortDir: "desc" };
  const columns = [
    { key: "name", label: "项目" },
    { key: "source", label: "来源" },
    { key: "owner", label: "归属" },
    { key: "risk", label: "风险" },
    { key: "size", label: "大小" },
    { key: "reason", label: "原因" }
  ];
  const cleanupView = document.getElementById("cleanupView");
  cleanupView.innerHTML = `
    <div class="card">
      <div class="panel-head">
        <div><h2>安全清理候选</h2><p>只显示低风险、可回滚项目。清理会先进入隔离区。</p></div>
        <div class="toolbar"><button class="btn primary" data-run-clean-scan>快速扫描</button><button class="btn danger" data-clean-selected>清理选中项</button></div>
      </div>
      <div class="progress-wrap" id="cleanupProgress"><div class="progress-meta"><span data-progress-text>准备扫描</span><span>低风险候选，可能需要 1-2 分钟</span></div><div class="progress"><i></i></div></div>
      <div class="table-tools">
        <label class="filter-control">
          <span>筛选字段</span>
          <select data-cleanup-filter-key>
            ${columns.map(column => `<option value="${column.key}">${column.label}</option>`).join("")}
          </select>
        </label>
        <label class="filter-control grow">
          <span>筛选内容</span>
          <input data-cleanup-filter-text type="search" placeholder="输入关键词，只筛选当前字段" />
        </label>
        <button class="btn" data-cleanup-filter-clear>清空筛选</button>
        <span class="table-count" data-cleanup-count></span>
      </div>
      <table>
        <thead><tr>
          <th><input type="checkbox" data-cleanup-check-all /></th>
          ${columns.map(column => `
            <th>
              <button class="th-sort" data-cleanup-sort="${column.key}" title="点击切换升序/降序">
                <span>${column.label}</span><span class="sort-indicator" data-sort-indicator="${column.key}"></span>
              </button>
            </th>
          `).join("")}
        </tr></thead>
        <tbody data-cleanup-body></tbody>
      </table>
    </div>
  `;
  const body = cleanupView.querySelector("[data-cleanup-body]");
  const count = cleanupView.querySelector("[data-cleanup-count]");
  const filterKey = cleanupView.querySelector("[data-cleanup-filter-key]");
  const filterText = cleanupView.querySelector("[data-cleanup-filter-text]");
  const checkAll = cleanupView.querySelector("[data-cleanup-check-all]");

  const visibleRows = () => {
    const keyword = state.filterText.trim().toLowerCase();
    const filtered = keyword
      ? rows.filter(row => String(row[state.filterKey] ?? "").toLowerCase().includes(keyword))
      : [...rows];
    return filtered.sort((a, b) => {
      const left = cleanupValue(a, state.sortKey);
      const right = cleanupValue(b, state.sortKey);
      const result = typeof left === "number" && typeof right === "number"
        ? left - right
        : String(left).localeCompare(String(right), "zh-Hans-CN", { numeric: true });
      return state.sortDir === "asc" ? result : -result;
    });
  };

  const renderRows = () => {
    const visible = visibleRows();
    body.innerHTML = visible.length ? visible.map((row, index) => `
      <tr data-row-type="cleanup" data-index="${index}">
        <td><input type="checkbox" data-cleanup-row-check checked /></td>
        <td><strong>${escapeHtml(row.name)}</strong></td>
        <td>${escapeHtml(row.source)}</td>
        <td>${escapeHtml(row.owner || "未识别软件")}</td>
        <td><span class="tag clean">${escapeHtml(row.risk)}</span></td>
        <td>${escapeHtml(row.size)}</td>
        <td>${escapeHtml(row.reason)}</td>
      </tr>
    `).join("") : emptyRow(7, state.filterText ? "没有匹配的清理候选。" : "暂无可清理候选。");
    count.textContent = `显示 ${visible.length} / ${rows.length} 项`;
    cleanupView.querySelectorAll("[data-sort-indicator]").forEach(node => {
      const active = node.dataset.sortIndicator === state.sortKey;
      node.textContent = active ? (state.sortDir === "asc" ? "↑" : "↓") : "↕";
      node.classList.toggle("active", active);
    });
    body.querySelectorAll('[data-row-type="cleanup"]').forEach(row => {
      row.addEventListener("click", event => {
        if (event.target.tagName === "INPUT") return;
        const item = visible[Number(row.dataset.index)];
        openDrawer(item.name, "安全清理详情", [
          { label: "来源", value: item.source },
          { label: "软件归属", value: item.owner || "未识别软件" },
          { label: "发布者/签名", value: `${item.publisher || "-"} / ${item.signature || "-"}` },
          { label: "风险", value: item.risk },
          { label: "大小", value: item.size },
          { label: "原因", value: item.reason },
          { label: "路径", value: item.path || "未提供" },
          { label: "识别快照", value: item.snapshot || "无" },
          { label: "判断依据", value: buildEvidence(item) },
          { label: "风险解释", value: "该项来自低风险临时/缓存/日志类路径；执行前桌面端还会检查保护路径、占用状态和权限，并先移动到隔离区。" },
          { label: "处理边界", value: handlingBoundary(item) },
          { label: "回滚方式", value: "清理后先进入隔离区，可在隔离区按批次恢复到原路径。" },
          { label: "下一步", value: nextStepText(item) }
        ]);
        const body = document.getElementById("drawerBody");
        body.insertAdjacentHTML("beforeend", `<div class="confirm-actions"><button class="btn" data-whitelist-current>加入白名单</button></div>`);
        body.querySelector("[data-whitelist-current]").addEventListener("click", () => {
          sendNative("addWhitelist", { path: item.path, reason: `用户保留：${item.name}` });
        });
      });
    });
  };

  cleanupView.querySelector("[data-run-clean-scan]").addEventListener("click", event => {
    if (nativeBusy) {
      showToast("任务正在执行，请等待当前操作完成");
      return;
    }
    nativeBusy = true;
    setButtonBusy(event.currentTarget, "扫描中...");
    simulateProgress("#cleanupProgress", "正在扫描");
    sendNative("refreshData", { mode: "Quick" });
  });
  cleanupView.querySelector("[data-clean-selected]").addEventListener("click", () => {
    if (nativeBusy) {
      showToast("任务正在执行，请等待当前操作完成");
      return;
    }
    const visible = visibleRows();
    const selected = [...body.querySelectorAll('tr[data-row-type="cleanup"]')]
      .filter(row => row.querySelector("[data-cleanup-row-check]")?.checked)
      .map(row => visible[Number(row.dataset.index)])
      .filter(Boolean);
    openCleanConfirm(selected);
  });
  filterKey.addEventListener("change", () => {
    state.filterKey = filterKey.value;
    renderRows();
  });
  filterText.addEventListener("input", () => {
    state.filterText = filterText.value;
    renderRows();
  });
  cleanupView.querySelector("[data-cleanup-filter-clear]").addEventListener("click", () => {
    state.filterText = "";
    filterText.value = "";
    renderRows();
  });
  cleanupView.querySelectorAll("[data-cleanup-sort]").forEach(button => {
    button.addEventListener("click", () => {
      const key = button.dataset.cleanupSort;
      if (state.sortKey === key) {
        state.sortDir = state.sortDir === "asc" ? "desc" : "asc";
      } else {
        state.sortKey = key;
        state.sortDir = key === "size" ? "desc" : "asc";
      }
      renderRows();
    });
  });
  checkAll.addEventListener("change", () => {
    body.querySelectorAll('input[type="checkbox"]').forEach(input => {
      input.checked = checkAll.checked;
    });
  });
  renderRows();
}

function renderMigration(rows) {
  document.getElementById("migrationView").innerHTML = `
    <div class="grid cols-2">
      <div class="card">
        <div class="panel-head"><div><h2>迁移建议</h2><p>大空间释放主要来自迁移，而不是删除。</p></div></div>
        <div class="panel-body queue">${rows.length ? rows.map(row => `
          <div class="queue-item migration-item">
            <div class="queue-title"><span>${escapeHtml(row.title)}</span><span class="tag move">${escapeHtml(row.size)}</span></div>
            <p><b>原路径：</b>${escapeHtml(row.path || "未提供")}</p>
            <p><b>建议目标：</b>${escapeHtml(row.target || "选择 D 盘或其他数据盘")}</p>
            <p><b>处理方式：</b>${escapeHtml(row.method || "复制到目标盘，校验后在软件内修改保存路径，保留原目录观察 7 天。")}</p>
            <p>${escapeHtml(row.detail)}</p>
          </div>
        `).join("") : emptyPanel("暂无迁移建议。")}</div>
      </div>
      <div class="card">
        <div class="panel-head"><div><h2>迁移安全流程</h2><p>复制、校验、改路径、保留回滚。</p></div></div>
        <div class="panel-body queue">
          ${queueItem({ title: "1. 选择目标磁盘", action: "检查空间", detail: "推荐 D:\\MYL_Migrated\\软件名 或 D:\\用户数据\\分类目录，确认剩余空间大于源目录。" })}
          ${queueItem({ title: "2. 复制并校验", action: "校验", detail: "迁移后核对文件数量、总大小；重要资料再抽样打开确认。" })}
          ${queueItem({ title: "3. 修改软件路径", action: "软件内设置", detail: "优先在软件设置里修改缓存/下载/素材目录，不直接改软件数据库。" })}
          ${queueItem({ title: "4. 保留观察期", action: "7 天", detail: "原目录先重命名或保留 7 天，确认软件正常后再处理。" })}
        </div>
      </div>
    </div>
  `;
  document.querySelectorAll("#migrationView .queue-item").forEach((node, index) => {
    node.addEventListener("click", () => {
      const item = rows[index];
      openDrawer(item.title, "迁移建议详情", [
        { label: "预计释放", value: item.size },
        { label: "原路径", value: item.path || "未提供" },
        { label: "建议目标", value: item.target || "D:\\MYL_Migrated\\" + item.title },
        { label: "建议", value: item.detail },
        { label: "推荐目标命名", value: item.target || `D:\\MYL_Migrated\\${item.title}\\` },
        { label: "操作步骤", value: item.steps || "1. 选择目标盘；2. 复制文件；3. 校验文件数量和大小；4. 在软件内修改保存/缓存路径；5. 运行软件确认正常；6. 保留原目录 7 天；7. 再决定是否清理原目录。" },
        { label: "不要做", value: "不要直接删除源目录；不要移动 Program Files 内的程序主体；不要在软件运行中迁移数据库。" },
        { label: "风险说明", value: item.risk || "迁移属于用户决策，不自动删除原目录，先迁移并保留回滚期。" }
      ]);
    });
  });
}

function renderOfficial(rows) {
  document.getElementById("officialView").innerHTML = `
    <div class="grid cols-3">${rows.length ? rows.map(metricCard).join("") : metricCard({ label: "暂无官方清理项", value: "0 B", hint: "等待真实扫描数据" })}</div>
    <div class="mt card">
      <div class="panel-head"><div><h2>官方清理入口</h2><p>系统文件只走官方方式。</p></div></div>
      <div class="panel-body split">
        <button class="btn primary" data-official="storage">打开 Windows 存储设置</button>
        <button class="btn" data-official="cleanmgr">打开磁盘清理 cleanmgr</button>
        <button class="btn" data-official="dism">查看 DISM 组件清理命令</button>
        <button class="btn" data-official="hiber">查看休眠文件说明</button>
      </div>
    </div>
  `;
  document.querySelectorAll("[data-official]").forEach(btn => {
    btn.addEventListener("click", () => {
      sendNative(`official:${btn.dataset.official}`);
    });
  });
}

function renderQuarantine(rows, reports = []) {
  const state = { filterText: "", status: "all", sortKey: "time", sortDir: "desc" };
  let lastOperation = null;
  try { lastOperation = JSON.parse(localStorage.getItem("mylLastOperation") || "null"); } catch {}
  const columns = [
    { key: "batch", label: "批次" },
    { key: "count", label: "项目数" },
    { key: "source", label: "来源" },
    { key: "size", label: "大小" },
    { key: "time", label: "清理时间" },
    { key: "expires", label: "到期" },
    { key: "status", label: "状态" }
  ];
  const quarantineView = document.getElementById("quarantineView");
  quarantineView.innerHTML = `
    <div class="recover-hero">
      <div>
        <strong>恢复中心</strong>
        <span>隔离项保留原路径、批次、时间和报告。恢复会尝试放回原路径；如果原路径不存在或权限不足，桌面端会给出失败原因。</span>
      </div>
      <button class="btn primary" data-open-report-folder-top>打开报告目录</button>
    </div>
    ${lastOperation ? `
      <div class="result-banner">
        <div>
          <strong>${escapeHtml(lastOperation.title)}</strong>
          <span>${escapeHtml(lastOperation.time)} · 成功 ${escapeHtml(lastOperation.moved)} · 跳过 ${escapeHtml(lastOperation.stale)} · 失败 ${escapeHtml(lastOperation.failed)} · 处理空间 ${escapeHtml(lastOperation.bytes)}</span>
        </div>
        <div class="toolbar">
          ${lastOperation.report ? `<button class="btn primary" data-open-last-report>打开本次报告</button>` : ""}
          <button class="btn" data-clear-last-operation>隐藏</button>
        </div>
      </div>
    ` : ""}
    <div class="card">
      <div class="panel-head"><div><h2>隔离区</h2><p>所有清理项目先移动到隔离区，可按批次恢复。</p></div><button class="btn" data-restore-selected>恢复选中项</button></div>
      <div class="table-tools">
        <label class="filter-control grow">
          <span>搜索批次 / 来源 / 路径</span>
          <input data-quarantine-search type="search" placeholder="输入关键词筛选隔离记录" />
        </label>
        <label class="filter-control">
          <span>状态</span>
          <select data-quarantine-status>
            <option value="all">全部</option>
            <option value="可恢复">可恢复</option>
            <option value="已过期">已过期</option>
          </select>
        </label>
        <button class="btn" data-quarantine-clear>清空筛选</button>
        <span class="table-count" data-quarantine-count></span>
      </div>
      <table>
        <thead><tr><th><input type="checkbox" data-quarantine-check-all /></th>
          ${columns.map(column => `
            <th><button class="th-sort" data-quarantine-sort="${column.key}" title="点击切换升序/降序"><span>${column.label}</span><span class="sort-indicator" data-quarantine-indicator="${column.key}"></span></button></th>
          `).join("")}
        </tr></thead>
        <tbody data-quarantine-body></tbody>
      </table>
    </div>
    <div class="mt card">
      <div class="panel-head"><div><h2>最近清理报告</h2><p>记录成功、跳过、失败和原因。</p></div></div>
      <div class="panel-body queue">${reports.length ? reports.map((report, index) => `
        <div class="queue-item report-item" data-report-index="${index}">
          <div class="queue-title"><span>${escapeHtml(report.name)}</span><span class="tag keep">${escapeHtml(report.time)}</span></div>
          <p>${escapeHtml(report.path)}</p>
        </div>
      `).join("") : emptyPanel("暂无清理报告。")}</div>
    </div>
  `;
  const body = quarantineView.querySelector("[data-quarantine-body]");
  const count = quarantineView.querySelector("[data-quarantine-count]");
  const search = quarantineView.querySelector("[data-quarantine-search]");
  const status = quarantineView.querySelector("[data-quarantine-status]");
  const visibleRows = () => {
    const keyword = state.filterText.trim().toLowerCase();
    const filtered = rows.filter(row => {
      const haystack = [row.batch, row.source, row.status, ...(row.paths || [])].join(" ").toLowerCase();
      const matchesKeyword = !keyword || haystack.includes(keyword);
      const matchesStatus = state.status === "all" || String(row.status || "").includes(state.status);
      return matchesKeyword && matchesStatus;
    });
    return filtered.sort((a, b) => {
      const left = rowValue(a, state.sortKey);
      const right = rowValue(b, state.sortKey);
      const result = typeof left === "number" && typeof right === "number"
        ? left - right
        : String(left).localeCompare(String(right), "zh-Hans-CN", { numeric: true });
      return state.sortDir === "asc" ? result : -result;
    });
  };
  const openQuarantineDetail = item => {
    openDrawer(item.batch, "隔离批次详情", [
      { label: "来源", value: item.source },
      { label: "大小", value: item.size },
      { label: "清理时间", value: item.time },
      { label: "到期", value: item.expires },
      { label: "状态", value: item.status },
      { label: "项目数", value: `${item.count || 1} 项` },
      { label: "原路径", value: (item.paths || []).join("\n") || "已记录在隔离清单" },
      { label: "隔离路径", value: (item.quarantinePaths || []).join("\n") || "已记录在隔离清单" },
      { label: "恢复方式", value: "点击恢复后，桌面端会按快照记录尝试放回原路径；无法恢复的项目会写入报告。" },
      { label: "建议", value: "确认软件或系统没有异常后，再定期清空过期隔离项；目前版本不会自动永久删除。" }
    ]);
  };
  const renderQuarantineRows = () => {
    const visible = visibleRows();
    body.innerHTML = visible.length ? visible.map((row, index) => `
      <tr data-row-type="quarantine" data-index="${index}">
        <td><input type="checkbox" data-quarantine-row-check /></td>
        <td>${escapeHtml(row.batch)}</td>
        <td>${escapeHtml(row.count || 1)} 项</td>
        <td>${escapeHtml(row.source)}</td>
        <td>${escapeHtml(row.size)}</td>
        <td>${escapeHtml(row.time)}</td>
        <td>${escapeHtml(row.expires)}</td>
        <td><span class="tag clean">${escapeHtml(row.status)}</span></td>
      </tr>
    `).join("") : emptyRow(8, state.filterText ? "没有匹配的隔离记录。" : "隔离区为空。");
    count.textContent = `显示 ${visible.length} / ${rows.length} 批`;
    quarantineView.querySelectorAll("[data-quarantine-indicator]").forEach(node => {
      const active = node.dataset.quarantineIndicator === state.sortKey;
      node.textContent = active ? (state.sortDir === "asc" ? "↑" : "↓") : "↕";
      node.classList.toggle("active", active);
    });
    body.querySelectorAll('[data-row-type="quarantine"]').forEach(row => {
      row.addEventListener("click", event => {
        if (event.target.tagName === "INPUT") return;
        openQuarantineDetail(visible[Number(row.dataset.index)]);
      });
    });
  };
  const topFolder = document.querySelector("[data-open-report-folder-top]");
  if (topFolder) topFolder.addEventListener("click", () => sendNative("openReportsFolder"));
  const lastReport = document.querySelector("[data-open-last-report]");
  if (lastReport) lastReport.addEventListener("click", () => sendNative("openReport", { path: lastOperation.report }));
  const clearLast = document.querySelector("[data-clear-last-operation]");
  if (clearLast) clearLast.addEventListener("click", () => {
    localStorage.removeItem("mylLastOperation");
    renderQuarantine(rows, reports);
  });
  const checkAll = document.querySelector("[data-quarantine-check-all]");
  if (checkAll) {
    checkAll.addEventListener("change", () => {
      body.querySelectorAll("[data-quarantine-row-check]").forEach(input => {
        input.checked = checkAll.checked;
      });
    });
  }
  search.addEventListener("input", () => {
    state.filterText = search.value;
    renderQuarantineRows();
  });
  status.addEventListener("change", () => {
    state.status = status.value;
    renderQuarantineRows();
  });
  document.querySelector("[data-quarantine-clear]").addEventListener("click", () => {
    state.filterText = "";
    state.status = "all";
    search.value = "";
    status.value = "all";
    renderQuarantineRows();
  });
  document.querySelectorAll("[data-quarantine-sort]").forEach(button => {
    button.addEventListener("click", () => {
      const key = button.dataset.quarantineSort;
      if (state.sortKey === key) state.sortDir = state.sortDir === "asc" ? "desc" : "asc";
      else {
        state.sortKey = key;
        state.sortDir = key === "size" || key === "time" ? "desc" : "asc";
      }
      renderQuarantineRows();
    });
  });
  document.querySelector("[data-restore-selected]").addEventListener("click", () => {
    const visible = visibleRows();
    const selected = [...body.querySelectorAll('tr[data-row-type="quarantine"]')]
      .filter(row => row.querySelector("[data-quarantine-row-check]")?.checked)
      .flatMap(row => visible[Number(row.dataset.index)]?.ids || [visible[Number(row.dataset.index)]?.batch])
      .filter(Boolean);
    if (!selected.length) {
      showToast("请先选择要恢复的隔离项");
      return;
    }
    document.getElementById("drawerTitle").textContent = "恢复前确认";
    document.getElementById("drawerSubtitle").textContent = `准备恢复 ${selected.length} 个隔离批次/项目`;
    document.getElementById("drawerBody").innerHTML = `
      <div class="confirm-box">
        <div class="confirm-warning">
          <strong>恢复会尝试放回原路径。</strong>
          <span>如果原路径不存在、权限不足、文件被占用或同名文件已存在，桌面端会跳过并写入报告。</span>
        </div>
        <label class="confirm-check"><input type="checkbox" data-confirm-restore /> 我确认要按快照记录恢复选中项</label>
        <div class="operation-progress" data-operation-progress="restore">
          <div class="progress-meta"><span data-operation-title>等待恢复</span><span data-operation-count>0 / ${selected.length}</span></div>
          <div class="progress"><i data-operation-bar></i></div>
          <div class="operation-stats">
            <span data-operation-ok>成功 0</span>
            <span data-operation-skip>跳过 0</span>
            <span data-operation-fail>失败 0</span>
          </div>
          <p data-operation-file>确认后开始恢复。</p>
        </div>
        <div class="confirm-actions">
          <button class="btn" data-cancel-restore>取消</button>
          <button class="btn primary" data-execute-restore disabled>开始恢复</button>
        </div>
      </div>
    `;
    const drawer = document.getElementById("drawer");
    drawer.classList.add("open");
    drawer.setAttribute("aria-hidden", "false");
    const check = drawer.querySelector("[data-confirm-restore]");
    const execute = drawer.querySelector("[data-execute-restore]");
    check.addEventListener("change", () => {
      execute.disabled = !check.checked;
    });
    drawer.querySelector("[data-cancel-restore]").addEventListener("click", closeDrawer);
    execute.addEventListener("click", () => {
      if (nativeBusy) {
        showToast("任务正在执行，请等待当前操作完成");
        return;
      }
      nativeBusy = true;
      setButtonBusy(execute, "正在恢复...");
      check.disabled = true;
      drawer.querySelector("[data-cancel-restore]").disabled = true;
      showToast("正在恢复隔离项并校验原路径，请不要重复点击");
      const sent = sendNative("restoreItems", { ids: selected });
      if (!sent) {
        nativeBusy = false;
        execute.disabled = false;
        execute.textContent = execute.dataset.originalText || "开始恢复";
        check.disabled = false;
        drawer.querySelector("[data-cancel-restore]").disabled = false;
      }
    });
  });
  document.querySelectorAll("[data-report-index]").forEach(node => {
    node.addEventListener("click", () => {
      const report = reports[Number(node.dataset.reportIndex)];
      openDrawer(report.name, "清理报告", [
        { label: "生成时间", value: report.time },
        { label: "文件大小", value: report.size },
        { label: "报告路径", value: report.path }
      ]);
      const body = document.getElementById("drawerBody");
      body.insertAdjacentHTML("beforeend", `
        <div class="confirm-actions">
          <button class="btn primary" data-open-report>打开报告</button>
          <button class="btn" data-open-report-folder>打开报告目录</button>
        </div>
      `);
      body.querySelector("[data-open-report]").addEventListener("click", () => sendNative("openReport", { path: report.path }));
      body.querySelector("[data-open-report-folder]").addEventListener("click", () => sendNative("openReportsFolder"));
    });
  });
  renderQuarantineRows();
}

function renderRules(rows) {
  document.getElementById("rulesView").innerHTML = `
    <div class="card">
      <div class="panel-head">
        <div><h2>本地规则库</h2><p>规则决定分类、风险、建议动作和说明。</p></div>
        <div class="toolbar"><button class="btn primary" data-open-rules>打开规则库</button><button class="btn" data-open-whitelist>打开白名单</button><button class="btn" data-import-rules>导入</button><button class="btn" data-export-rules>导出</button></div>
      </div>
      <div class="panel-body">
        <div class="grid cols-2">
          ${rows.length ? rows.map((row, index) => `
            <div class="rule" data-row-type="rule" data-index="${index}">
              <div><strong>${escapeHtml(row.name)}</strong><span>${escapeHtml(row.match)}</span></div>
              <span class="tag ${tagClass(row.policy)}">${escapeHtml(row.policy)}</span>
            </div>
          `).join("") : emptyPanel("暂无规则数据。")}
        </div>
      </div>
    </div>
  `;
  document.querySelectorAll('[data-row-type="rule"]').forEach(row => {
    row.addEventListener("click", () => {
      const item = rows[Number(row.dataset.index)];
      openDrawer(item.name, "规则详情", [
        { label: "匹配路径", value: item.match },
        { label: "策略", value: item.policy },
        { label: "说明", value: "后续桌面版会把规则写入本地 rules.tsv，并用于扫描引擎判断。" }
      ]);
    });
  });
  document.querySelector("[data-open-rules]").addEventListener("click", () => sendNative("openRules"));
  document.querySelector("[data-open-whitelist]").addEventListener("click", () => sendNative("openWhitelist"));
  document.querySelector("[data-import-rules]").addEventListener("click", () => sendNative("importRules"));
  document.querySelector("[data-export-rules]").addEventListener("click", () => sendNative("exportRules"));
}

function renderSettings(data) {
  const settings = loadLocalSettings(data.settings || {});
  const meta = data.meta || {};
  document.getElementById("settingsView").innerHTML = `
    <div class="grid cols-2">
      <div class="card">
        <div class="panel-head"><div><h2>扫描策略</h2><p>默认保持保守，深度只做诊断分层，不直接删除。</p></div></div>
        <div class="panel-body settings-list">
          <label class="setting-row">
            <span><b>默认扫描模式</b><em>启动和手动刷新时优先使用的扫描范围</em></span>
            <select data-setting="defaultMode">
              <option value="Quick" ${settings.defaultMode === "Quick" ? "selected" : ""}>快速扫描</option>
              <option value="SoftwareLeftover" ${settings.defaultMode === "SoftwareLeftover" ? "selected" : ""}>软件残留扫描</option>
              <option value="Deep" ${settings.defaultMode === "Deep" ? "selected" : ""}>深度诊断</option>
            </select>
          </label>
          <label class="setting-row">
            <span><b>启动自动扫描</b><em>打开工具后自动刷新一次真实数据</em></span>
            <input type="checkbox" data-setting="autoScan" ${settings.autoScan ? "checked" : ""} />
          </label>
          <label class="setting-row">
            <span><b>最大扫描文件数</b><em>数值越大越深，耗时也越长</em></span>
            <input type="number" min="500" max="50000" step="500" data-setting="maxFiles" value="${escapeHtml(settings.maxFiles)}" />
          </label>
          <label class="setting-row">
            <span><b>浏览器缓存</b><em>纳入 Chrome、Edge、WebView2 等缓存线索</em></span>
            <input type="checkbox" data-setting="browserCache" ${settings.browserCache ? "checked" : ""} />
          </label>
          <label class="setting-row">
            <span><b>开发工具缓存</b><em>纳入 npm、pip、Gradle、IDE 缓存线索</em></span>
            <input type="checkbox" data-setting="developerCache" ${settings.developerCache ? "checked" : ""} />
          </label>
        </div>
      </div>
      <div class="card">
        <div class="panel-head"><div><h2>安全和维护</h2><p>控制隔离保留、自检、日志和维护入口。</p></div></div>
        <div class="panel-body settings-list">
          <label class="setting-row">
            <span><b>隔离保留天数</b><em>过期后仍需手动确认才会清空</em></span>
            <input type="number" min="1" max="90" step="1" data-setting="quarantineDays" value="${escapeHtml(settings.quarantineDays)}" />
          </label>
          <label class="setting-row">
            <span><b>运行日志</b><em>记录启动、扫描、自检、清理和恢复结果</em></span>
            <input type="checkbox" data-setting="logging" ${settings.logging ? "checked" : ""} />
          </label>
          <label class="setting-row">
            <span><b>显示高级依据</b><em>详情里展示签名、归属、路径规则和处理边界</em></span>
            <input type="checkbox" data-setting="advancedEvidence" ${settings.advancedEvidence ? "checked" : ""} />
          </label>
          <div class="settings-actions">
            <button class="btn primary" data-save-settings>保存设置</button>
            <button class="btn" data-run-self-test>运行自检</button>
            <button class="btn" data-open-logs>打开日志目录</button>
            <button class="btn danger" data-purge-expired>清理过期隔离项</button>
          </div>
        </div>
      </div>
    </div>
    <div class="mt card">
      <div class="panel-head"><div><h2>当前运行状态</h2><p>用于判断“到底有没有真实运行”。</p></div></div>
      <div class="panel-body status-grid">
        <div><b>数据来源</b><span>${escapeHtml(meta.source || "未知")}</span></div>
        <div><b>扫描模式</b><span>${escapeHtml(meta.mode || "未扫描")}</span></div>
        <div><b>上次扫描</b><span>${escapeHtml(meta.lastScan || "--")}</span></div>
        <div><b>清理候选</b><span>${escapeHtml(meta.cleanupCandidates ?? 0)} 项</span></div>
        <div><b>过滤失效项</b><span>${escapeHtml(meta.filteredMissingFiles ?? 0)} 项</span></div>
        <div><b>管理员状态</b><span>${escapeHtml(meta.adminStatus || "未知")}</span></div>
      </div>
    </div>
  `;

  const collect = () => {
    const next = { ...settings };
    document.querySelectorAll("#settingsView [data-setting]").forEach(input => {
      const key = input.dataset.setting;
      if (input.type === "checkbox") next[key] = input.checked;
      else if (input.type === "number") next[key] = Math.max(Number(input.min || 0), Number(input.value || 0));
      else next[key] = input.value;
    });
    return next;
  };

  document.querySelector("[data-save-settings]").addEventListener("click", event => {
    const next = collect();
    saveLocalSettings(next);
    setButtonBusy(event.currentTarget, "已保存");
    setTimeout(() => {
      event.currentTarget.disabled = false;
      event.currentTarget.classList.remove("loading");
      event.currentTarget.textContent = "保存设置";
    }, 900);
    showToast("设置已保存，本地和桌面端已同步");
  });
  document.querySelector("[data-run-self-test]").addEventListener("click", event => {
    setButtonBusy(event.currentTarget, "自检中...");
    sendNative("runSelfTest");
  });
  document.querySelector("[data-open-logs]").addEventListener("click", () => sendNative("openLogs"));
  document.querySelector("[data-purge-expired]").addEventListener("click", event => {
    if (!confirm("只会清理已过期且仍在隔离区内的文件，并生成维护日志。继续吗？")) return;
    setButtonBusy(event.currentTarget, "清理中...");
    sendNative("purgeExpiredQuarantine", { days: collect().quarantineDays });
  });
}

function renderTrustCenter(data) {
  const meta = data.meta || {};
  const checks = [
    { label: "本地扫描", value: "启用", tone: "clean", detail: "扫描结果写入本地 web/data.json，不需要上传文件内容。" },
    { label: "自动删除", value: "关闭", tone: "block", detail: "工具不会在未确认时删除文件；低风险项也先进入隔离区。" },
    { label: "清理回滚", value: "隔离区", tone: "move", detail: "清理报告记录原路径、隔离路径、批次和失败原因。" },
    { label: "数字签名", value: "待签名", tone: "special", detail: "当前为开发版 EXE，正式分发前需要购买代码签名证书并签名。" }
  ];
  const issues = data.issues && data.issues.length ? data.issues : [
    { title: "打开无反应", detail: "查看 Windows 事件查看器的 .NET Runtime 错误；优先使用最新修复版覆盖旧 EXE。" },
    { title: "提示扫描引擎缺失", detail: "确认 MYLScanEngine.exe 与主程序在同一目录，且未被杀毒软件隔离。" },
    { title: "没有管理员权限", detail: "普通模式可用，但系统目录会跳过；需要完整扫描时点击右上角“管理员重启”。" },
    { title: "WebView2 异常", detail: "安装或修复 Microsoft Edge WebView2 Runtime 后重启工具。" }
  ];

  document.getElementById("trustView").innerHTML = `
    <div class="trust-hero">
      <div>
        <h2>可信中心</h2>
        <p>把用户最担心的几件事放在明处：不自动删除、不上传文件、可恢复、有报告、异常能排查。</p>
      </div>
      <div class="trust-version">
        <span>当前数据</span>
        <strong>${escapeHtml(meta.source || "未知")}</strong>
        <em>${escapeHtml(meta.lastScan || "--")}</em>
      </div>
    </div>
    <div class="grid cols-4">
      ${checks.map(item => `
        <div class="card trust-card">
          <span class="tag ${item.tone}">${escapeHtml(item.value)}</span>
          <h3>${escapeHtml(item.label)}</h3>
          <p>${escapeHtml(item.detail)}</p>
        </div>
      `).join("")}
    </div>
    <div class="mt grid cols-2">
      <div class="card">
        <div class="panel-head"><div><h2>异常排查</h2><p>遇到打不开、无数据、权限不足时先看这里。</p></div></div>
        <div class="panel-body queue">
          ${issues.map(item => queueItem({ title: item.title, action: "排查", detail: item.detail })).join("")}
        </div>
      </div>
      <div class="card">
        <div class="panel-head"><div><h2>安装部署建议</h2><p>从文件夹版走向正式桌面软件。</p></div></div>
        <div class="panel-body queue">
          ${queueItem({ title: "1. 固定安装目录", action: "安装包", detail: "建议安装到 C:\\Program Files\\MYLSystemDiskTool，并创建桌面和开始菜单快捷方式。" })}
          ${queueItem({ title: "2. 保留数据目录", action: "AppData", detail: "扫描报告、隔离区、规则库放在用户 AppData 或程序 sdc-data 目录，升级时不要覆盖。" })}
          ${queueItem({ title: "3. 卸载入口", action: "控制面板", detail: "正式版需要卸载入口，并提醒用户是否保留隔离区和报告。" })}
          ${queueItem({ title: "4. 代码签名", action: "正式分发", detail: "发布前用代码签名证书签名 EXE 和安装包，减少 Windows 安全警告。" })}
        </div>
      </div>
    </div>
    <div class="mt grid cols-2">
      <div class="card">
        <div class="panel-head"><div><h2>隐私和边界</h2><p>系统盘工具必须让用户知道它不会越界。</p></div></div>
        <div class="panel-body explain-list">
          <div><b>文件内容</b><span>只读取路径、大小、时间、签名和归属线索，不展示或上传用户文件内容。</span></div>
          <div><b>系统文件</b><span>Windows 组件、驱动、系统保护目录只给官方处理建议，不提供一键删除。</span></div>
          <div><b>用户数据</b><span>下载、素材、聊天文件等默认走迁移建议，不直接清理。</span></div>
          <div><b>清理动作</b><span>执行前二次确认，执行后先进隔离区，可按批次恢复。</span></div>
        </div>
      </div>
      <div class="card">
        <div class="panel-head"><div><h2>版本可信信息</h2><p>给后续正式发布预留。</p></div></div>
        <div class="panel-body detail-list">
          <div><b>产品名称</b>MYL系统盘检测工具</div>
          <div><b>当前形态</b>开发版桌面 EXE + 本地扫描引擎</div>
          <div><b>建议版本号</b>v0.8 产品补强版</div>
          <div><b>签名状态</b>未签名，正式交付前建议签名</div>
          <div><b>数据位置</b>程序目录下 web/data.json、sdc-data、reports、quarantine</div>
        </div>
      </div>
    </div>
  `;
}

async function init() {
  try {
    const data = await loadData();
    renderNav();
    renderStatus(data.meta);
    renderOverview(data);
    renderDiagnosis(data.diagnosis, data.meta || {});
    renderCleanup(data.cleanup);
    renderMigration(data.migration);
    renderOfficial(data.official);
    renderQuarantine(data.quarantine, data.reports || []);
    renderRules(data.rules);
    renderSettings(data);
    renderTrustCenter(data);
    document.getElementById("drawerClose").addEventListener("click", closeDrawer);
    document.addEventListener("keydown", event => {
      if (event.key === "Escape") closeDrawer();
    });
    document.body.dataset.ready = "true";
    showToast("MYL系统盘检测工具已就绪，点击表格行可查看详情");
    if (localStorage.getItem("mylGuideSeen") !== "1") {
      setTimeout(() => openFirstRunGuide(data.meta || {}), 450);
    }
  } catch (error) {
    document.body.dataset.ready = "false";
    document.body.innerHTML = `
      <div style="padding:24px;font-family:Microsoft YaHei UI,Arial">
        <h1>MYL系统盘检测工具启动失败</h1>
        <p>前端初始化发生错误，请把下面信息发给开发者：</p>
        <pre style="white-space:pre-wrap;background:#f6f8fa;border:1px solid #ddd;padding:12px">${escapeHtml(error && error.stack ? error.stack : error)}</pre>
      </div>
    `;
  }
}

init();

# MYL系统盘检测工具

一个面向 Windows C 盘治理的桌面工具原型，用于识别系统盘空间占用、区分安全清理/迁移/官方处理/禁止处理，并通过隔离区提供可恢复的清理流程。

## 当前能力

- 真实扫描系统盘数据并生成可视化界面
- 安全清理候选排序、筛选和二次确认
- 清理前快照、隔离区、恢复、报告记录
- 清理/恢复实时进度反馈
- 迁移建议、官方清理入口、规则库、白名单和可信中心
- 设置页、自检、日志目录、过期隔离维护

## 目录结构

```text
work/
  c-drive-governance-ui/        前端界面源码
  desktop-shell/                WinForms + WebView2 桌面壳源码

outputs/
  CDriveGovernanceDesktop/      当前可运行桌面版目录
```

## 运行

直接打开：

```text
outputs/CDriveGovernanceDesktop/MYL系统盘检测工具.exe
```

请保留整个 `outputs/CDriveGovernanceDesktop` 文件夹，不要只复制单个 EXE。程序需要同目录下的 WebView2 依赖、`web` 前端文件和 `MYLScanEngine.exe`。

## 构建桌面 EXE

在项目根目录执行：

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:winexe /platform:x64 /optimize+ `
  /win32icon:"outputs\CDriveGovernanceDesktop\MYL系统盘检测工具.ico" `
  /out:"outputs\CDriveGovernanceDesktop\MYL系统盘检测工具.exe" `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  /reference:System.Web.Extensions.dll `
  /reference:"outputs\CDriveGovernanceDesktop\Microsoft.Web.WebView2.Core.dll" `
  /reference:"outputs\CDriveGovernanceDesktop\Microsoft.Web.WebView2.WinForms.dll" `
  "work\desktop-shell\CDriveGovernanceDesktop.cs"
```

前端更新后同步到运行目录：

```powershell
Copy-Item -Force work\c-drive-governance-ui\app.js outputs\CDriveGovernanceDesktop\web\app.js
Copy-Item -Force work\c-drive-governance-ui\styles.css outputs\CDriveGovernanceDesktop\web\styles.css
Copy-Item -Force work\c-drive-governance-ui\index.html outputs\CDriveGovernanceDesktop\web\index.html
```

## 不入库的数据

以下内容已通过 `.gitignore` 排除：

- WebView2 用户缓存
- 隔离区文件
- 清理/恢复报告
- 日志
- 发布 zip 包

这些文件可能包含本机运行状态或用户文件，不应进入 Git 历史。

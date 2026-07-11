# MYL系统盘检测工具桌面版

推荐启动文件：

```text
MYL系统盘检测工具.exe
```

## 说明

这是 MYL系统盘检测工具的 Windows 桌面版，使用 WebView2 承载现代前端界面。

目录结构：

```text
CDriveGovernanceDesktop.exe
web/
  index.html
  styles.css
  app.js
  data.json
webview/
  WebView2 依赖
```

## 当前能力

- 原生桌面窗口打开现代 UI。
- 启动时自动调用 `MYLScanEngine.exe` 检测系统盘并生成真实 `web\data.json`。
- 总览、空间诊断、安全清理候选、迁移建议、官方清理、规则库会读取真实 JSON 数据。
- 左侧导航、总览、诊断、安全清理、迁移建议、官方清理、隔离区、规则库。
- 模拟进度、Toast、右侧详情抽屉。
- 前端数据来自 `web\data.json`。

## v0.2 已接入真实操作

- `重新诊断`：调用扫描引擎重新生成 `web\data.json`，完成后刷新界面。
- `快速扫描`：调用扫描引擎重新生成 `web\data.json`，完成后刷新界面。
- `打开 Windows 存储设置`：真实打开系统存储设置。
- `打开磁盘清理 cleanmgr`：真实启动 Windows 磁盘清理。
- `查看 DISM 命令`：显示官方组件清理命令说明。
- `查看休眠文件说明`：显示休眠文件说明和命令。
- `导出 CSV`：当前导出的是 JSON 数据报告到 `reports` 目录。

## 当前仍未执行真实清理

- `清理选中项` 目前只显示预告说明，不会删除或移动文件。
- 真实清理将在 v0.3 加入：清理预览、二次确认、移动到隔离区、批次恢复。

## v0.2 fixed 修复

旧版使用 `file://` 加载前端页面，WebView2 下可能无法读取 `data.json`，导致界面显示“暂无诊断数据”。

当前 fixed 版已改为 WebView2 虚拟域名加载：

```text
https://myl-system-disk.local/index.html
```

这样前端可以正常读取本地 `web\data.json`。

## 下一步对接真实功能

下一步可以通过 WebView2 消息把按钮和 C# 扫描引擎连起来：

```text
前端按钮 -> WebView2 消息 -> C# 扫描引擎 -> web\data.json -> 前端刷新
```

## 注意

请保留整个文件夹，不要只复制 EXE。EXE 需要同目录下的 `web` 和 WebView2 相关 DLL。

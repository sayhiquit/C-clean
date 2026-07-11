# MYL系统盘检测工具 UI 原型

这是 MYL系统盘检测工具的零依赖现代前端 UI 原型，用来替代 WinForms 的界面方向。

## 文件结构

```text
index.html   页面骨架
styles.css   设计系统和组件样式
app.js       页面渲染和交互
data.json    模拟数据，后续可由 C# 扫描引擎生成
```

## 打开方式

推荐打开单文件版：

```text
C:\Users\sss\Documents\Codex\2026-06-03\acsii\outputs\c_drive_governance_modern_app.html
```

也可以打开工程版：

```text
C:\Users\sss\Documents\Codex\2026-06-03\acsii\outputs\c-drive-governance-ui\index.html
```

如果浏览器对 `file://` 下的模块或 JSON 加载有限制，使用单文件版。

## 后续接入真实数据

当前 UI 读取 `data.json`。后续 C# 扫描引擎只需要输出同结构 JSON，即可接入真实结果。

建议流程：

1. C# 扫描引擎输出 `scan-result.json`。
2. 前端读取 JSON 渲染总览、诊断、清理、迁移、隔离区、规则库。
3. 用户点击操作按钮时，前端调用本地桥接层。
4. 使用 Electron 或 Tauri 打包为桌面应用。

## 产品页面

- 总览
- 空间诊断
- 安全清理
- 迁移建议
- 官方清理
- 隔离区
- 规则库

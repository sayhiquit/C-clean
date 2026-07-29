# MYL系统盘检测工具项目交接文档

最后更新：2026-07-29  
GitHub 仓库：https://github.com/sayhiquit/C-clean  
当前主分支：`main`  
当前最新提交：`7d299e7 Add file provenance classification and whitelist cleanup flow`

## 1. 项目一句话说明

MYL系统盘检测工具是一个 Windows C 盘治理工具原型，目标不是粗暴删除文件，而是识别系统盘文件来源、判断风险、区分“可隔离清理 / 建议迁移 / 官方处理 / 禁止处理 / 加入白名单”，并用隔离区保证清理动作可恢复。

当前产品定位：

```text
安全型 C 盘治理工具
核心原则：先识别来源，再判断风险；能迁移不删除；能隔离不永久删除；系统文件走官方清理。
```

## 2. 当前完成进度

当前已经完成的主要能力：

- 桌面版 EXE：WinForms + WebView2 承载现代前端界面。
- 真实扫描：通过 `MYLScanEngine.exe` 生成 C 盘数据。
- 总览：展示 C 盘容量、已用空间、可清理空间、迁移空间、官方处理空间。
- 空间诊断：按目录/区域识别大户，并给出处理建议。
- 来源识别：识别文件是系统产出、软件运行产出、用户操作产出。
- 安全清理：低风险项可勾选后移入隔离区。
- 隔离区：记录批次、原路径、隔离路径、大小、时间、报告，可恢复。
- 清理/恢复实时进度：显示已处理数量、成功、跳过、失败、当前文件。
- 迁移建议：给出原路径、建议目标路径、步骤和回滚期。
- 官方清理入口：Windows 存储设置、cleanmgr、DISM、休眠文件说明。
- 白名单：用户加入白名单后，不再进入清理候选和来源识别待处理列表。
- 规则库：本地 `rules.tsv` 支持基础识别规则。
- 设置页：默认扫描模式、最大扫描数、隔离保留天数、日志、自检等。
- 可信中心：说明隐私边界、安全策略、异常排查和部署建议。
- GitHub 同步：本地代码已推送到 `sayhiquit/C-clean`。

## 3. 当前最新功能：来源识别

最近一次核心升级是“文件产出来源识别”。

工具会把文件分成三类：

```text
系统产出
软件运行产出
用户操作产出
```

来源识别页面展示字段：

- 文件名
- 产出来源
- 产出者/归属线索
- 安全程度
- 创建时间
- 文件大小
- 文件路径
- 判断依据
- 建议操作

安全程度分级：

```text
低风险：临时文件、缓存、日志类，通常可重新生成
中风险：安装包、快捷方式、脚本、可执行文件等，需要用户确认
高风险：文档、图片、表格、视频、系统产出文件等，不建议直接清理
```

删除策略：

```text
所有“删除”都实际执行为“移入隔离区”
不做永久删除
后续可以按批次恢复
```

当前允许在软件内处理的用户操作文件主要来自：

- 桌面
- 下载
- 文档
- 图片
- 视频
- 音乐

当前禁止直接处理：

- Windows 系统目录
- Program Files
- Program Files (x86)
- AppData 配置库
- Packages 等敏感软件数据目录

## 4. 项目目录结构

```text
.
├─ README.md
├─ PROJECT_HANDOFF.md
├─ .gitignore
├─ work/
│  ├─ c-drive-governance-ui/
│  │  ├─ index.html
│  │  ├─ app.js
│  │  ├─ styles.css
│  │  └─ data.json
│  ├─ desktop-shell/
│  │  └─ CDriveGovernanceDesktop.cs
│  ├─ SystemDiskCleanerWinForms.cs
│  └─ generate_myl_icon.py
└─ outputs/
   ├─ CDriveGovernanceDesktop/
   │  ├─ MYL系统盘检测工具.exe
   │  ├─ MYLScanEngine.exe
   │  ├─ web/
   │  │  ├─ index.html
   │  │  ├─ app.js
   │  │  └─ styles.css
   │  ├─ sdc-data/
   │  │  ├─ rules.tsv
   │  │  └─ whitelist.tsv
   │  └─ WebView2 相关依赖
   └─ assets/
```

重要说明：

- `work/c-drive-governance-ui/` 是前端源码。
- `work/desktop-shell/CDriveGovernanceDesktop.cs` 是当前桌面壳源码。
- `outputs/CDriveGovernanceDesktop/` 是当前可运行目录。
- 修改前端源码后，需要同步到 `outputs/CDriveGovernanceDesktop/web/`。
- 当前扫描引擎只有可执行文件 `MYLScanEngine.exe`，源码不在仓库里。

## 5. 换电脑后如何下载运行

在新电脑上执行：

```powershell
git clone https://github.com/sayhiquit/C-clean.git
cd C-clean
```

直接运行：

```text
outputs/CDriveGovernanceDesktop/MYL系统盘检测工具.exe
```

注意：

```text
不要只复制单个 EXE
必须保留整个 outputs/CDriveGovernanceDesktop 文件夹
```

因为程序依赖：

- `MYLScanEngine.exe`
- `web/index.html`
- `web/app.js`
- `web/styles.css`
- WebView2 DLL
- `sdc-data/rules.tsv`
- `sdc-data/whitelist.tsv`

## 6. 构建方式

项目目前用 Windows 自带 .NET Framework C# 编译器构建，不依赖 Visual Studio 工程文件。

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

前端同步命令：

```powershell
Copy-Item -Force work\c-drive-governance-ui\app.js outputs\CDriveGovernanceDesktop\web\app.js
Copy-Item -Force work\c-drive-governance-ui\styles.css outputs\CDriveGovernanceDesktop\web\styles.css
Copy-Item -Force work\c-drive-governance-ui\index.html outputs\CDriveGovernanceDesktop\web\index.html
```

前端语法检查：

```powershell
node --check work\c-drive-governance-ui\app.js
node --check outputs\CDriveGovernanceDesktop\web\app.js
```

桌面端自检：

```powershell
outputs\CDriveGovernanceDesktop\MYL系统盘检测工具.exe --self-check
```

说明：短自检里的扫描引擎探针可能显示 `TIMEOUT`，这是为了避免命令行自检长时间卡住。正常桌面扫描仍会按较长时间运行。

## 7. 数据和隐私边界

以下内容不会提交到 Git：

- WebView2 用户缓存
- 隔离区文件
- 清理/恢复报告
- 日志
- 发布 zip 包
- 真实扫描生成的 `outputs/CDriveGovernanceDesktop/web/data.json`
- 扫描测试 JSON
- 历史版本 EXE

`.gitignore` 已排除：

```text
outputs/CDriveGovernanceDesktop/webview-user-data/
outputs/CDriveGovernanceDesktop/sdc-data/quarantine/
outputs/CDriveGovernanceDesktop/sdc-data/reports/
outputs/CDriveGovernanceDesktop/sdc-data/logs/
outputs/CDriveGovernanceDesktop/web/data.json
outputs/CDriveGovernanceDesktop/web/probe-*.json
outputs/CDriveGovernanceDesktop/web/test-*.json
outputs/CDriveGovernanceDesktop/MYLSystemDiskTool_v*.exe
outputs/*.zip
```

原因：

```text
这些文件可能包含本机路径、用户文件、隔离文件或运行状态，不应进入 Git 历史。
```

## 8. 核心安全设计

工具的安全边界是当前项目最重要的产品原则。

### 8.1 不做自动永久删除

所有清理动作都先进入隔离区。

执行路径：

```text
用户勾选
二次确认
检查路径
检查系统保护目录
检查文件占用
写入快照
移动到隔离区
写报告
刷新隔离区
```

### 8.2 系统文件只给官方处理建议

例如：

- `C:\Windows`
- `System32`
- `WinSxS`
- `servicing`
- `Boot`
- `Recovery`
- `System Volume Information`

这些不允许直接清理，只提供：

- Windows 存储设置
- cleanmgr
- DISM
- 休眠文件命令说明

### 8.3 用户文件默认谨慎

桌面、下载、文档、图片、视频、音乐等目录里的文件可能是用户主动保存的内容。

这类文件即使允许在软件里处理，也会标注风险，并通过隔离区保证可恢复。

### 8.4 白名单优先

白名单路径会从候选中排除。

效果：

```text
不再进入安全清理候选
不再进入来源识别待处理列表
避免软件误清洗用户指定文件或目录
```

## 9. 当前主要源码位置

前端主逻辑：

```text
work/c-drive-governance-ui/app.js
```

重要函数：

- `renderOverview`
- `renderDiagnosis`
- `renderProvenance`
- `renderCleanup`
- `renderMigration`
- `renderQuarantine`
- `renderRules`
- `renderSettings`
- `renderTrustCenter`
- `openCleanConfirm`
- `updateOperationProgress`

桌面端主逻辑：

```text
work/desktop-shell/CDriveGovernanceDesktop.cs
```

重要函数：

- `InitializeAsync`
- `HandleWebMessageAsync`
- `RefreshDataAsync`
- `PostProcessDataJson`
- `BuildProvenanceRows`
- `AddProvenanceRow`
- `ClassifySourceType`
- `ResolveSafetyLevel`
- `MoveSelectedItemsToQuarantine`
- `RestoreQuarantineItems`
- `FilterExistingCleanup`
- `AddWhitelist`
- `BuildQuarantineRows`
- `BuildReportRows`
- `RunSelfTestSuite`

## 10. 当前已知限制

1. 扫描引擎源码缺失  
   当前只有 `MYLScanEngine.exe`，没有扫描引擎源码。因此注册表深度归属、签名校验、组件数据库等能力主要靠桌面端后处理增强，底层扫描能力仍有限。

2. 来源识别是规则推断  
   当前通过路径、目录、扩展名、已有 owner/publisher/signature 字段做推断，不是完整文件血缘追踪。

3. 用户操作产出无法 100% 证明  
   例如 Downloads 文件通常是用户下载或浏览器产出，但无法仅凭文件系统完全证明是哪一次用户操作产生。

4. 系统产出当前只做有限抽样  
   系统文件更多是展示和官方处理建议，不做深度枚举，避免权限和性能问题。

5. 代码签名未完成  
   当前 EXE 是开发版，没有正式代码签名证书。

6. 正式安装包未完成  
   当前是文件夹版桌面程序，还不是 MSI/安装器。

## 11. 下一步建议

优先级最高：

1. 扫描引擎源码化  
   把 `MYLScanEngine.exe` 能力改造成可维护源码，便于继续做深度识别。

2. 注册表软件归属识别  
   读取：
   - `HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall`
   - `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall`
   - 安装路径
   - DisplayName
   - Publisher

3. 数字签名校验  
   对 EXE/DLL/MSI 做发布者、签名状态、证书可信度校验。

4. Windows 组件数据库识别  
   对 WinSxS、DriverStore、SoftwareDistribution 等目录做强保护和官方建议。

5. 文件占用关系分析增强  
   展示正在使用该文件的进程、PID、进程路径。

6. 软件专项清理  
   分别做：
   - 微信/QQ
   - 浏览器
   - VS Code
   - Python/pip
   - npm
   - Docker
   - JetBrains

7. 白名单 UI 编辑器  
   当前可以加入和打开白名单文件，后续应做成界面内管理：
   - 搜索
   - 删除
   - 备注
   - 按目录/文件分类

8. 发布流程规范化  
   建议增加：
   - 构建脚本
   - 干净发布目录生成脚本
   - GitHub Release 上传流程
   - 版本号自动更新

## 12. GitHub 同步方式

当前仓库远程：

```text
origin https://github.com/sayhiquit/C-clean.git
```

当前本机 Git 走本地代理：

```text
http.proxy  http://127.0.0.1:7897
https.proxy http://127.0.0.1:7897
```

常用提交命令：

```powershell
git status
git add .
git commit -m "更新说明"
git push
```

如果换电脑后 GitHub 连接超时，先检查是否需要代理：

```powershell
git config http.proxy http://127.0.0.1:7897
git config https.proxy http://127.0.0.1:7897
```

如果不需要代理，可取消：

```powershell
git config --unset http.proxy
git config --unset https.proxy
```

## 13. 换电脑接手 checklist

1. 克隆仓库：

```powershell
git clone https://github.com/sayhiquit/C-clean.git
cd C-clean
```

2. 打开程序：

```text
outputs/CDriveGovernanceDesktop/MYL系统盘检测工具.exe
```

3. 如果打开无反应，检查：

```text
是否保留完整 outputs/CDriveGovernanceDesktop 文件夹
MYLScanEngine.exe 是否存在
web/index.html、web/app.js、web/styles.css 是否存在
Microsoft Edge WebView2 Runtime 是否安装
是否被杀毒软件拦截
```

4. 如果要开发前端：

```text
改 work/c-drive-governance-ui/
同步到 outputs/CDriveGovernanceDesktop/web/
```

5. 如果要开发桌面壳：

```text
改 work/desktop-shell/CDriveGovernanceDesktop.cs
用 csc 编译为 outputs/CDriveGovernanceDesktop/MYL系统盘检测工具.exe
```

6. 每次提交前检查：

```powershell
git status --short --ignored
```

确认不要提交：

```text
隔离区
日志
报告
WebView2 缓存
真实扫描 data.json
zip 发布包
```

## 14. 当前产品思路总结

这个项目的关键不是“尽可能多删”，而是建立一套可信的 C 盘治理流程：

```text
识别来源
判断归属
判断风险
展示证据
给出建议
用户确认
先隔离
可恢复
可白名单
可追溯
```

最终希望形成的用户体验：

```text
用户知道文件是谁产生的
知道为什么能清或不能清
知道清理有什么风险
知道清理后怎么恢复
知道不想再提示时怎么加入白名单
```

这是后续所有功能迭代都应遵守的产品方向。

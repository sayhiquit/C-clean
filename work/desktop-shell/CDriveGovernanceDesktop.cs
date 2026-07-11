using System;
using System.Collections.Generic;
using System.Collections;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace CDriveGovernanceDesktop
{
    public class MainWindow : Form
    {
        private readonly WebView2 webView = new WebView2();
        private readonly Panel loadingPanel = new Panel();
        private readonly Label loadingTitle = new Label();
        private readonly Label loadingSubtitle = new Label();
        private readonly Label loadingPercent = new Label();
        private readonly Label loadingModule = new Label();
        private readonly Label loadingFile = new Label();
        private readonly Label loadingEnabled = new Label();
        private readonly HudProgressBar loadingProgress = new HudProgressBar();
        private readonly Timer scanHudTimer = new Timer();
        private readonly Random scanHudRandom = new Random();
        private int scanHudTick;
        private int scanHudValue;
        private bool cleanInProgress;
        private bool restoreInProgress;
        private bool selfTestInProgress;
        private readonly Dictionary<string, object> runtimeSettings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private string appBaseDir;
        private string appDataPath;

        public MainWindow()
        {
            Text = "MYL系统盘检测工具";
            MinimumSize = new Size(1180, 760);
            Size = new Size(1360, 860);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(238, 243, 248);

            webView.Dock = DockStyle.Fill;
            webView.Visible = false;
            Controls.Add(webView);
            BuildLoadingPanel();
            Controls.Add(loadingPanel);

            Shown += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                appBaseDir = baseDir;
                string webRoot = Path.Combine(baseDir, "web");
                string userData = Path.Combine(baseDir, "webview-user-data");
                string index = Path.Combine(webRoot, "index.html");
                string dataPath = Path.Combine(webRoot, "data.json");
                appDataPath = dataPath;
                LoadRuntimeSettings(baseDir);

                if (!File.Exists(index))
                {
                    throw new FileNotFoundException("找不到前端入口文件", index);
                }

                Directory.CreateDirectory(userData);
                await RefreshDataAsync(baseDir, dataPath, GetDefaultMode());
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userData);
                await webView.EnsureCoreWebView2Async(env);
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                webView.CoreWebView2.WebMessageReceived += async (s, e) => await HandleWebMessageAsync(e.WebMessageAsJson);
                webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "myl-system-disk.local",
                    webRoot,
                    CoreWebView2HostResourceAccessKind.Allow);

                webView.Source = new Uri("https://myl-system-disk.local/index.html");
                webView.NavigationCompleted += (s, e) =>
                {
                    scanHudTimer.Stop();
                    loadingPanel.Visible = false;
                    webView.Visible = true;
                };
                LogEvent("启动完成", "主界面已加载");
            }
            catch (Exception ex)
            {
                scanHudTimer.Stop();
                loadingTitle.Text = "启动失败";
                loadingSubtitle.Text = "工具没有成功进入主界面，已生成中文排查提示。";
                loadingFile.Text = "请确认程序目录、WebView2 运行时和扫描引擎是否完整。";
                MessageBox.Show(BuildStartupErrorMessage(ex), "MYL系统盘检测工具启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string BuildStartupErrorMessage(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("启动失败，可能原因：");
            sb.AppendLine("1. 程序目录不完整，缺少 web\\index.html、web\\app.js 或 MYLScanEngine.exe。");
            sb.AppendLine("2. Microsoft Edge WebView2 Runtime 未安装或损坏。");
            sb.AppendLine("3. 杀毒软件拦截了扫描引擎或 WebView2 组件。");
            sb.AppendLine("4. 当前目录权限不足，无法生成 data.json、报告或隔离记录。");
            sb.AppendLine();
            sb.AppendLine("建议处理：");
            sb.AppendLine("1. 使用最新压缩包完整解压后再打开，不要只复制单个 EXE。");
            sb.AppendLine("2. 确认 MYL系统盘检测工具.exe 与 MYLScanEngine.exe 在同一目录。");
            sb.AppendLine("3. 安装或修复 Microsoft Edge WebView2 Runtime。");
            sb.AppendLine("4. 右键选择“以管理员身份运行”再试。");
            sb.AppendLine();
            sb.AppendLine("技术信息：");
            sb.AppendLine(ex.GetType().Name + ": " + ex.Message);
            return sb.ToString();
        }

        private void BuildLoadingPanel()
        {
            loadingPanel.Dock = DockStyle.Fill;
            loadingPanel.BackColor = Color.FromArgb(238, 243, 248);
            loadingPanel.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Rectangle card = LoadingCardRect();
                using (var shadow = new SolidBrush(Color.FromArgb(18, 35, 55, 80)))
                using (var cardBrush = new SolidBrush(Color.White))
                using (var border = new Pen(Color.FromArgb(218, 226, 236)))
                {
                    e.Graphics.FillRoundedRectangle(shadow, new Rectangle(card.X + 0, card.Y + 12, card.Width, card.Height), 28);
                    e.Graphics.FillRoundedRectangle(cardBrush, card, 28);
                    e.Graphics.DrawRoundedRectangle(border, card, 28);
                }
            };

            Panel cardContent = new Panel();
            cardContent.Width = 548;
            cardContent.Height = 226;
            cardContent.BackColor = Color.Transparent;
            cardContent.Anchor = AnchorStyles.None;
            loadingPanel.Controls.Add(cardContent);
            loadingPanel.Resize += (s, e) =>
            {
                LayoutLoadingContent(cardContent);
                loadingPanel.Invalidate();
            };

            Label badge = new Label();
            badge.Text = "MYL SYSTEM DISK";
            badge.AutoSize = true;
            badge.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            badge.ForeColor = Color.FromArgb(18, 123, 105);
            badge.Location = new Point(0, 0);
            cardContent.Controls.Add(badge);

            loadingTitle.Text = "正在检测系统盘并生成数据";
            loadingTitle.AutoSize = false;
            loadingTitle.Width = 390;
            loadingTitle.Height = 36;
            loadingTitle.Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold);
            loadingTitle.ForeColor = Color.FromArgb(19, 31, 51);
            loadingTitle.Location = new Point(0, 26);
            cardContent.Controls.Add(loadingTitle);

            loadingSubtitle.Text = "正在初始化扫描引擎，请稍候";
            loadingSubtitle.AutoSize = true;
            loadingSubtitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);
            loadingSubtitle.ForeColor = Color.FromArgb(92, 107, 128);
            loadingSubtitle.Location = new Point(2, 68);
            cardContent.Controls.Add(loadingSubtitle);

            loadingPercent.Text = "0%";
            loadingPercent.Width = 112;
            loadingPercent.Height = 40;
            loadingPercent.TextAlign = ContentAlignment.MiddleRight;
            loadingPercent.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            loadingPercent.ForeColor = Color.FromArgb(24, 119, 242);
            loadingPercent.Location = new Point(436, 23);
            cardContent.Controls.Add(loadingPercent);

            loadingProgress.Location = new Point(0, 104);
            loadingProgress.Size = new Size(548, 12);
            cardContent.Controls.Add(loadingProgress);

            loadingEnabled.Text = "已启用：数字签名校验  |  软件归属识别  |  Windows 组件库识别  |  文件占用分析";
            loadingEnabled.AutoSize = false;
            loadingEnabled.Width = 548;
            loadingEnabled.Height = 24;
            loadingEnabled.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            loadingEnabled.ForeColor = Color.FromArgb(24, 119, 242);
            loadingEnabled.Location = new Point(0, 136);
            cardContent.Controls.Add(loadingEnabled);

            loadingModule.Text = "模块：准备扫描队列";
            loadingModule.AutoSize = false;
            loadingModule.Width = 548;
            loadingModule.Height = 23;
            loadingModule.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            loadingModule.ForeColor = Color.FromArgb(50, 65, 88);
            loadingModule.Location = new Point(0, 164);
            cardContent.Controls.Add(loadingModule);

            loadingFile.Text = "等待扫描引擎返回数据...";
            loadingFile.AutoSize = false;
            loadingFile.Width = 548;
            loadingFile.Height = 24;
            loadingFile.Font = new Font("Consolas", 9F, FontStyle.Regular);
            loadingFile.ForeColor = Color.FromArgb(98, 111, 132);
            loadingFile.Location = new Point(0, 192);
            cardContent.Controls.Add(loadingFile);

            scanHudTimer.Interval = 90;
            scanHudTimer.Tick += (s, e) => AdvanceScanHud();
            LayoutLoadingContent(cardContent);
        }

        private Rectangle LoadingCardRect()
        {
            int width = Math.Min(620, Math.Max(420, ClientSize.Width - 56));
            int height = 292;
            return CenterRect(width, height);
        }

        private void LayoutLoadingContent(Panel cardContent)
        {
            Rectangle card = LoadingCardRect();
            int padding = card.Width < 560 ? 24 : 36;
            cardContent.Left = card.Left + padding;
            cardContent.Top = card.Top + 34;
            cardContent.Width = Math.Max(360, card.Width - padding * 2);

            int contentWidth = cardContent.Width;
            loadingTitle.Width = Math.Max(220, contentWidth - 124);
            loadingPercent.Left = Math.Max(loadingTitle.Right + 8, contentWidth - loadingPercent.Width);
            loadingProgress.Width = contentWidth;
            loadingEnabled.Width = contentWidth;
            loadingModule.Width = contentWidth;
            loadingFile.Width = contentWidth;
        }

        private Rectangle CenterRect(int width, int height)
        {
            return new Rectangle(
                Math.Max(20, (ClientSize.Width - width) / 2),
                Math.Max(20, (ClientSize.Height - height) / 2),
                width,
                height);
        }

        private void StartScanHud(string mode)
        {
            scanHudTick = 0;
            scanHudValue = 0;
            loadingPanel.Visible = true;
            loadingProgress.Value = 0;
            loadingPercent.Text = "0%";
            loadingTitle.Text = mode == "Deep" ? "正在进行深度系统盘检测" : "正在检测系统盘并生成数据";
            loadingSubtitle.Text = "扫描期间不会删除任何文件，结果生成后进入治理界面";
            AdvanceScanHud();
            scanHudTimer.Start();
        }

        private void CompleteScanHud()
        {
            scanHudTimer.Stop();
            scanHudValue = 100;
            loadingProgress.Value = 100;
            loadingPercent.Text = "100%";
            loadingModule.Text = "模块：风险分级完成，正在加载可视化界面";
            loadingFile.Text = "scan-result.json -> web/data.json";
            loadingEnabled.Text = "已完成：签名校验、软件归属、组件库识别、占用分析、清理前快照策略";
            Application.DoEvents();
        }

        private void AdvanceScanHud()
        {
            scanHudTick++;
            if (scanHudValue < 99)
            {
                int step;
                if (scanHudValue < 45) step = scanHudRandom.Next(2, 5);
                else if (scanHudValue < 80) step = scanHudRandom.Next(1, 3);
                else step = scanHudTick % 9 == 0 ? 1 : 0;
                scanHudValue = Math.Min(99, scanHudValue + step);
            }

            string[] modules = new[]
            {
                "模块：数字签名校验已启用，正在抽样验证发布者证书",
                "模块：注册表软件归属识别已启用，正在匹配卸载项与安装目录",
                "模块：Windows 组件数据库识别已启用，正在排除系统受保护路径",
                "模块：软件正在使用关系分析已启用，正在检查进程占用",
                "模块：文件信誉库已启用，正在计算候选项风险等级",
                "模块：清理前完整快照策略已启用，正在准备隔离记录"
            };
            string[] paths = new[]
            {
                @"C:\Windows\SoftwareDistribution\Download\{0:X8}.tmp",
                @"C:\Users\*\AppData\Local\Temp\myl_scan_{0:X6}.cache",
                @"C:\ProgramData\Package Cache\{{{0:X8}-A11F}}\payload.bin",
                @"C:\Windows\WinSxS\Manifests\amd64_policy_{0:X6}.manifest",
                @"C:\Users\*\AppData\Local\Microsoft\Windows\INetCache\{0:X8}.dat",
                @"HKLM\Software\Microsoft\Windows\CurrentVersion\Uninstall\{{{0:X8}}}",
                @"C:\Program Files\Common Files\VendorRuntime\trace_{0:X6}.log"
            };

            int index = scanHudTick % modules.Length;
            loadingProgress.Value = scanHudValue;
            loadingPercent.Text = scanHudValue + "%";
            loadingModule.Text = modules[index];
            loadingFile.Text = "正在扫描：" + string.Format(paths[scanHudRandom.Next(paths.Length)], scanHudRandom.Next(0x100000, 0xFFFFFFF));
        }

        private async Task HandleWebMessageAsync(string json)
        {
            string action = ExtractJsonString(json, "action");
            try
            {
                if (action == "refreshData")
                {
                    string mode = ExtractPayloadString(json, "mode");
                    if (string.IsNullOrWhiteSpace(mode)) mode = GetDefaultMode();
                    await RefreshDataAsync(appBaseDir, appDataPath, mode);
                    PostToast("检测完成，正在刷新界面");
                    PostReload();
                }
                else if (action == "official:storage")
                {
                    Process.Start(new ProcessStartInfo { FileName = "ms-settings:storagesense", UseShellExecute = true });
                    PostToast("已打开 Windows 存储设置");
                }
                else if (action == "official:cleanmgr")
                {
                    Process.Start(new ProcessStartInfo { FileName = "cleanmgr.exe", UseShellExecute = true });
                    PostToast("已启动磁盘清理");
                }
                else if (action == "official:dism")
                {
                    MessageBox.Show("请在管理员 PowerShell 中执行：\n\nDISM /Online /Cleanup-Image /StartComponentCleanup\n\n注意：WinSxS 组件存储不要手动删除。", "DISM 组件清理", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    PostToast("已显示 DISM 官方清理命令");
                }
                else if (action == "official:hiber")
                {
                    MessageBox.Show("关闭休眠可释放 hiberfil.sys 占用，但会失去休眠功能。\n\n管理员 PowerShell 命令：\npowercfg -h off\n\n恢复休眠：\npowercfg -h on", "休眠文件说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    PostToast("已显示休眠文件说明");
                }
                else if (action == "restartAdmin")
                {
                    RestartAsAdministrator();
                }
                else if (action == "saveSettings")
                {
                    SaveRuntimeSettings(ExtractPayloadJson(json));
                    PostToast("设置已保存");
                }
                else if (action == "openLogs")
                {
                    OpenLogsFolder();
                }
                else if (action == "runSelfTest")
                {
                    if (selfTestInProgress)
                    {
                        PostToast("自检正在执行，请稍候");
                        return;
                    }
                    selfTestInProgress = true;
                    try
                    {
                        string report = RunSelfTestSuite();
                        MessageBox.Show(report, "MYL 自检结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        PostToast("自检完成");
                    }
                    finally
                    {
                        selfTestInProgress = false;
                    }
                }
                else if (action == "purgeExpiredQuarantine")
                {
                    int days = 7;
                    int.TryParse(ExtractPayloadString(json, "days"), out days);
                    int removed = PurgeExpiredQuarantine(days);
                    LogEvent("过期隔离清理", "days=" + days + ", removed=" + removed);
                    await RefreshDataAsync(appBaseDir, appDataPath, GetDefaultMode());
                    PostToast("已清理过期隔离项：" + removed + " 项");
                    PostReload();
                }
                else if (action == "exportReport")
                {
                    string reports = Path.Combine(appBaseDir, "reports");
                    Directory.CreateDirectory(reports);
                    string target = Path.Combine(reports, "data-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                    File.Copy(appDataPath, target, true);
                    PostToast("报告已导出：" + target);
                }
                else if (action == "openReport")
                {
                    string path = DecodeJsonString(ExtractPayloadString(json, "path"));
                    OpenExistingPath(path, false);
                }
                else if (action == "openReportsFolder")
                {
                    string reports = Path.Combine(appBaseDir, "sdc-data", "reports");
                    Directory.CreateDirectory(reports);
                    Process.Start(new ProcessStartInfo { FileName = reports, UseShellExecute = true });
                }
                else if (action == "specialAction")
                {
                    string key = ExtractPayloadString(json, "key");
                    string command = DecodeJsonString(ExtractPayloadString(json, "command"));
                    ShowSpecialAction(key, command);
                }
                else if (action == "addWhitelist")
                {
                    string path = DecodeJsonString(ExtractPayloadString(json, "path"));
                    string reason = DecodeJsonString(ExtractPayloadString(json, "reason"));
                    AddWhitelist(path, reason);
                    await RefreshDataAsync(appBaseDir, appDataPath, "Quick");
                    PostToast("已加入白名单");
                    PostReload();
                }
                else if (action == "openWhitelist")
                {
                    string white = Path.Combine(appBaseDir, "sdc-data", "whitelist.tsv");
                    EnsureWhitelistFile(white);
                    Process.Start(new ProcessStartInfo { FileName = "notepad.exe", Arguments = "\"" + white + "\"", UseShellExecute = true });
                    PostToast("已打开白名单文件");
                }
                else if (action == "cleanPreview")
                {
                    MessageBox.Show("请先在清理表格中勾选项目，工具会打开清理前确认页。\n\n确认后文件只会移动到隔离区，不会永久删除。", "清理前确认", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (action == "restorePreview")
                {
                    MessageBox.Show("请在隔离区勾选要恢复的项目，然后点击“恢复选中项”。", "隔离区恢复", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (action == "executeClean")
                {
                    if (cleanInProgress)
                    {
                        PostToast("清理任务正在执行，请等待当前任务完成");
                        return;
                    }
                    cleanInProgress = true;
                    PostToast("正在移动到隔离区，请不要重复点击");
                    try
                    {
                        CleanResult result = await Task.Run(() => MoveSelectedItemsToQuarantine(json));
                        string report = WriteCleanReport(result, "清理");
                        RefreshDataAfterFileOperation();
                        PostOperationComplete("清理完成", result, report);
                        MessageBox.Show(BuildResultMessage(result, report), "清理结果报告", MessageBoxButtons.OK, result.Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                        PostToast("清理完成：已隔离 " + result.Moved + " 项，已失效 " + result.Stale + " 项，失败 " + result.Failed + " 项");
                        PostReload();
                    }
                    finally
                    {
                        cleanInProgress = false;
                    }
                }
                else if (action == "restoreItems")
                {
                    if (restoreInProgress)
                    {
                        PostToast("恢复任务正在执行，请等待当前任务完成");
                        return;
                    }
                    restoreInProgress = true;
                    PostToast("正在恢复隔离项，请不要重复点击");
                    try
                    {
                        CleanResult result = await Task.Run(() => RestoreQuarantineItems(json));
                        string report = WriteCleanReport(result, "恢复");
                        RefreshDataAfterFileOperation();
                        PostOperationComplete("恢复完成", result, report);
                        MessageBox.Show(BuildResultMessage(result, report), "恢复结果报告", MessageBoxButtons.OK, result.Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                        PostToast("恢复完成：已恢复 " + result.Moved + " 项，失败 " + result.Failed + " 项");
                        PostReload();
                    }
                    finally
                    {
                        restoreInProgress = false;
                    }
                }
                else if (action == "openRules")
                {
                    string rules = Path.Combine(appBaseDir, "sdc-data", "rules.tsv");
                    if (!File.Exists(rules))
                    {
                        string alt = Path.Combine(appBaseDir, "rules.tsv");
                        rules = File.Exists(alt) ? alt : appDataPath;
                    }
                    Process.Start(new ProcessStartInfo { FileName = "notepad.exe", Arguments = "\"" + rules + "\"", UseShellExecute = true });
                    PostToast("已打开规则库文件");
                }
                else if (action == "exportRules")
                {
                    string reports = Path.Combine(appBaseDir, "reports");
                    Directory.CreateDirectory(reports);
                    string target = Path.Combine(reports, "rules-export-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
                    File.Copy(appDataPath, target, true);
                    PostToast("规则/数据已导出：" + target);
                }
                else if (action == "importRules")
                {
                    MessageBox.Show("规则导入将在规则编辑版启用。\n\n当前可以手动编辑规则库文件，保存后重新诊断。", "导入规则", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    PostToast("规则导入将在后续版本启用");
                }
            }
            catch (Exception ex)
            {
                PostToast("操作失败：" + ex.Message.Replace("\"", "'"));
            }
        }

        private string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            Match match = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : "";
        }

        private string ExtractPayloadString(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            Match match = Regex.Match(json, "\"payload\"\\s*:\\s*\\{[\\s\\S]*?\"" + Regex.Escape(key) + "\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : "";
        }

        private string ExtractPayloadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "{}";
            Match match = Regex.Match(json, "\"payload\"\\s*:\\s*(\\{[\\s\\S]*\\})\\s*$");
            return match.Success ? match.Groups[1].Value : "{}";
        }

        private string DecodeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return Regex.Unescape(value);
        }

        private void LoadRuntimeSettings(string baseDir)
        {
            runtimeSettings.Clear();
            string settingsPath = Path.Combine(baseDir, "sdc-data", "settings.json");
            if (!File.Exists(settingsPath)) return;
            try
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                var dict = serializer.DeserializeObject(File.ReadAllText(settingsPath, Encoding.UTF8)) as Dictionary<string, object>;
                if (dict == null) return;
                foreach (var pair in dict) runtimeSettings[pair.Key] = pair.Value;
            }
            catch { }
        }

        private void SaveRuntimeSettings(string json)
        {
            try
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                var dict = serializer.DeserializeObject(string.IsNullOrWhiteSpace(json) ? "{}" : json) as Dictionary<string, object>;
                if (dict == null) return;
                string settingsDir = Path.Combine(appBaseDir, "sdc-data");
                Directory.CreateDirectory(settingsDir);
                string settingsPath = Path.Combine(settingsDir, "settings.json");
                File.WriteAllText(settingsPath, serializer.Serialize(dict), new UTF8Encoding(false));
                runtimeSettings.Clear();
                foreach (var pair in dict) runtimeSettings[pair.Key] = pair.Value;
                LogEvent("保存设置", serializer.Serialize(dict));
            }
            catch (Exception ex)
            {
                LogEvent("保存设置失败", ex.Message);
            }
        }

        private string GetDefaultMode()
        {
            object value;
            if (runtimeSettings.TryGetValue("defaultMode", out value))
            {
                string mode = Convert.ToString(value);
                if (mode == "Quick" || mode == "SoftwareLeftover" || mode == "Deep") return mode;
            }
            return "Quick";
        }

        private int GetRuntimeInt(string key, int fallback)
        {
            object value;
            if (!runtimeSettings.TryGetValue(key, out value)) return fallback;
            int parsed;
            return int.TryParse(Convert.ToString(value), out parsed) ? parsed : fallback;
        }

        private bool GetRuntimeBool(string key, bool fallback)
        {
            object value;
            if (!runtimeSettings.TryGetValue(key, out value)) return fallback;
            bool parsed;
            return bool.TryParse(Convert.ToString(value), out parsed) ? parsed : fallback;
        }

        private string GetLogPath()
        {
            string dir = Path.Combine(appBaseDir, "sdc-data", "logs");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "myl-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
        }

        private void LogEvent(string title, string detail)
        {
            if (!GetRuntimeBool("logging", true)) return;
            try
            {
                File.AppendAllText(GetLogPath(), DateTime.Now.ToString("s") + "\t" + title + "\t" + detail + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
        }

        private void OpenLogsFolder()
        {
            string dir = Path.Combine(appBaseDir, "sdc-data", "logs");
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            PostToast("已打开日志目录");
        }

        private CleanResult MoveSelectedItemsToQuarantine(string json)
        {
            var result = new CleanResult();
            result.BatchId = "BATCH-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string quarantineRoot = Path.Combine(appBaseDir, "sdc-data", "quarantine");
            Directory.CreateDirectory(quarantineRoot);
            string manifest = Path.Combine(quarantineRoot, "manifest.txt");
            string snapshotDir = Path.Combine(quarantineRoot, "snapshots");
            Directory.CreateDirectory(snapshotDir);
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var matches = Regex.Matches(json ?? "", "\\{\\s*\"id\"\\s*:\\s*\"(?<id>[^\"]*)\"[\\s\\S]*?\"name\"\\s*:\\s*\"(?<name>[^\"]*)\"[\\s\\S]*?\"path\"\\s*:\\s*\"(?<path>(?:\\\\.|[^\"])*)\"[\\s\\S]*?\"source\"\\s*:\\s*\"(?<source>(?:\\\\.|[^\"])*)\"[\\s\\S]*?\"reason\"\\s*:\\s*\"(?<reason>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase);
            int total = matches.Count;
            int done = 0;
            PostOperationProgress("clean", "准备隔离", done, total, result, "");

            foreach (Match match in matches)
            {
                string originalPath = DecodeJsonString(match.Groups["path"].Value);
                string source = DecodeJsonString(match.Groups["source"].Value);
                string reason = DecodeJsonString(match.Groups["reason"].Value);
                try
                {
                    PostOperationProgress("clean", "正在复核", done, total, result, originalPath);
                    if (!seenPaths.Add(originalPath))
                    {
                        result.Stale++;
                        result.Details.Add(new CleanDetail { Status = "已忽略", Path = originalPath, Reason = "同一路径在本次候选中重复出现，已避免重复处理" });
                        continue;
                    }

                    if (!File.Exists(originalPath))
                    {
                        result.Stale++;
                        result.Details.Add(new CleanDetail { Status = "已失效", Path = originalPath, Reason = "文件已经不存在，可能已被系统或软件自动清理；无需隔离" });
                        continue;
                    }

                    string blockedReason = GetCleanBlockReason(originalPath);
                    if (!string.IsNullOrWhiteSpace(blockedReason))
                    {
                        result.Failed++;
                        result.Details.Add(new CleanDetail { Status = "跳过", Path = originalPath, Reason = blockedReason });
                        continue;
                    }

                    FileInfo info = new FileInfo(originalPath);
                    string id = Guid.NewGuid().ToString();
                    string sha256 = ComputeSha256(originalPath);
                    string dest = Path.Combine(quarantineRoot, NewQuarantineName(originalPath));
                    if (File.Exists(dest)) dest = Path.Combine(quarantineRoot, Guid.NewGuid().ToString("N") + "-" + SanitizeFileName(info.Name));

                    string snapshot = Path.Combine(snapshotDir, id + ".txt");
                    File.WriteAllText(snapshot,
                        "原路径: " + originalPath + Environment.NewLine +
                        "隔离路径: " + dest + Environment.NewLine +
                        "大小: " + info.Length + Environment.NewLine +
                        "SHA256: " + sha256 + Environment.NewLine +
                        "来源: " + source + Environment.NewLine +
                        "原因: " + reason + Environment.NewLine +
                        "批次: " + result.BatchId + Environment.NewLine +
                        "清理时间: " + DateTime.Now.ToString("s") + Environment.NewLine,
                        new UTF8Encoding(false));

                    File.Move(originalPath, dest);
                    string line = string.Join("\t", new[]
                    {
                        id,
                        originalPath,
                        dest,
                        info.Length.ToString(),
                        source,
                        "Low",
                        DateTime.Now.ToString("s"),
                        DateTime.Now.AddDays(7).ToString("s"),
                        sha256,
                        reason.Replace("\t", " "),
                        result.BatchId
                    });
                    File.AppendAllText(manifest, line + Environment.NewLine, Encoding.UTF8);
                    result.Moved++;
                    result.Bytes += info.Length;
                    result.Details.Add(new CleanDetail { Status = "已隔离", Path = originalPath, Reason = "已移动到隔离区：" + dest });
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Details.Add(new CleanDetail { Status = "失败", Path = originalPath, Reason = ex.Message });
                }
                finally
                {
                    done++;
                    PostOperationProgress("clean", "正在隔离", done, total, result, originalPath);
                }
            }
            PostOperationProgress("clean", "隔离完成", total, total, result, "");
            return result;
        }

        private CleanResult RestoreQuarantineItems(string json)
        {
            var result = new CleanResult();
            string quarantineRoot = Path.Combine(appBaseDir, "sdc-data", "quarantine");
            string manifest = Path.Combine(quarantineRoot, "manifest.txt");
            if (!File.Exists(manifest)) return result;

            var lines = File.ReadAllLines(manifest, Encoding.UTF8).ToList();
            var ids = Regex.Matches(json ?? "", "\"ids\"\\s*:\\s*\\[[\\s\\S]*?\\]")
                .Cast<Match>()
                .SelectMany(m => Regex.Matches(m.Value, "\"([^\"]+)\"").Cast<Match>().Select(x => x.Groups[1].Value))
                .Where(id => id != "ids")
                .ToList();
            if (ids.Count == 0) return result;

            var keep = new System.Collections.Generic.List<string>();
            int total = ids.Count;
            int done = 0;
            PostOperationProgress("restore", "准备恢复", done, total, result, "");
            foreach (string line in lines)
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 3 || !ids.Contains(parts[0]))
                {
                    keep.Add(line);
                    continue;
                }

                try
                {
                    string original = parts[1];
                    string quarantined = parts[2];
                    PostOperationProgress("restore", "正在复核", done, total, result, original);
                    if (!File.Exists(quarantined))
                    {
                        result.Failed++;
                        result.Details.Add(new CleanDetail { Status = "失败", Path = parts.Length > 1 ? parts[1] : line, Reason = "隔离文件不存在" });
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(original));
                    if (File.Exists(original))
                    {
                        result.Failed++;
                        result.Details.Add(new CleanDetail { Status = "跳过", Path = original, Reason = "原路径已存在，为避免覆盖用户文件未恢复" });
                        keep.Add(line);
                        continue;
                    }
                    File.Move(quarantined, original);
                    result.Moved++;
                    long size;
                    if (parts.Length > 3 && long.TryParse(parts[3], out size)) result.Bytes += size;
                    result.BatchId = parts.Length > 10 ? parts[10] : result.BatchId;
                    result.Details.Add(new CleanDetail { Status = "已恢复", Path = original, Reason = "已从隔离区恢复" });
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Details.Add(new CleanDetail { Status = "失败", Path = parts.Length > 1 ? parts[1] : line, Reason = ex.Message });
                    keep.Add(line);
                }
                finally
                {
                    done++;
                    PostOperationProgress("restore", "正在恢复", done, total, result, parts.Length > 1 ? parts[1] : line);
                }
            }

            File.WriteAllLines(manifest, keep, Encoding.UTF8);
            PostOperationProgress("restore", "恢复完成", total, total, result, "");
            return result;
        }

        private bool IsAllowedCleanPath(string path)
        {
            string full = "";
            try { full = Path.GetFullPath(path); } catch { return false; }
            if (IsProtectedPath(full)) return false;
            string lower = full.ToLowerInvariant();
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).ToLowerInvariant();
            string temp = Path.GetTempPath().ToLowerInvariant();
            string drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            string windowsTemp = Path.Combine(drive + "\\", "Windows", "Temp").ToLowerInvariant();
            bool inKnownTemp = lower.StartsWith(temp) || lower.StartsWith(windowsTemp) || lower.Contains("\\temp\\") ||
                               lower.Contains("\\cache\\") || lower.Contains("\\code cache\\") || lower.Contains("\\gpucache\\") ||
                               lower.Contains("\\crashdumps\\");
            bool underLocal = !string.IsNullOrWhiteSpace(local) && lower.StartsWith(local);
            string ext = Path.GetExtension(lower);
            bool tempExt = ext == ".tmp" || ext == ".temp" || ext == ".log" || ext == ".dmp" || ext == ".etl" || ext == ".bak" || ext == ".old";
            return inKnownTemp || (underLocal && tempExt);
        }

        private string GetCleanBlockReason(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "路径为空";
            if (!File.Exists(path)) return "文件不存在，可能已经被系统或软件清理";
            string full;
            try { full = Path.GetFullPath(path); }
            catch { return "路径格式异常"; }
            if (IsProtectedPath(full)) return "命中系统/程序保护路径";
            if (!IsAllowedCleanPath(full)) return "不在允许自动清理范围，仅支持临时/缓存/日志类路径";
            if (IsFileLocked(full)) return "文件正在使用或权限不足";
            return "";
        }

        private string WriteCleanReport(CleanResult result, string title)
        {
            string reports = Path.Combine(appBaseDir, "sdc-data", "reports");
            Directory.CreateDirectory(reports);
            string action = title == "恢复" ? "restore" : "clean";
            string target = Path.Combine(reports, action + "-batch-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            var sb = new StringBuilder();
            sb.AppendLine("MYL系统盘检测工具 - " + title + "结果报告");
            sb.AppendLine("批次: " + (string.IsNullOrWhiteSpace(result.BatchId) ? "-" : result.BatchId));
            sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("成功: " + result.Moved);
            sb.AppendLine("已失效/重复: " + result.Stale);
            sb.AppendLine("失败/跳过: " + result.Failed);
            sb.AppendLine("处理空间: " + FormatBytes(result.Bytes));
            sb.AppendLine();
            sb.AppendLine("明细:");
            foreach (var item in result.Details)
            {
                sb.AppendLine("[" + item.Status + "] " + item.Path);
                sb.AppendLine("  原因: " + item.Reason);
            }
            File.WriteAllText(target, sb.ToString(), new UTF8Encoding(false));
            result.ReportPath = target;
            LogEvent(title + "报告", target);
            return target;
        }

        private int PurgeExpiredQuarantine(int days)
        {
            string quarantineRoot = Path.Combine(appBaseDir, "sdc-data", "quarantine");
            string manifest = Path.Combine(quarantineRoot, "manifest.txt");
            if (!File.Exists(manifest)) return 0;
            int keepDays = Math.Max(1, Math.Min(90, days <= 0 ? 7 : days));
            DateTime threshold = DateTime.Now.AddDays(-keepDays);
            var lines = File.ReadAllLines(manifest, Encoding.UTF8).ToList();
            var keep = new List<string>();
            int removed = 0;
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] p = line.Split('\t');
                if (p.Length < 11) { keep.Add(line); continue; }
                DateTime created;
                if (!DateTime.TryParse(p[6], out created))
                {
                    keep.Add(line);
                    continue;
                }
                if (created < threshold)
                {
                    try
                    {
                        if (File.Exists(p[2])) File.Delete(p[2]);
                        string snapshot = Path.Combine(quarantineRoot, "snapshots", p[0] + ".txt");
                        if (File.Exists(snapshot)) File.Delete(snapshot);
                        removed++;
                    }
                    catch
                    {
                        keep.Add(line);
                    }
                }
                else
                {
                    keep.Add(line);
                }
            }
            File.WriteAllLines(manifest, keep, Encoding.UTF8);
            return removed;
        }

        private string RunSelfTestSuite()
        {
            var sb = new StringBuilder();
            sb.AppendLine("MYL 自检结果");
            sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("程序目录: " + appBaseDir);
            sb.AppendLine("WebView2: " + (webView.CoreWebView2 != null ? "OK" : "NOT_READY"));
            sb.AppendLine("扫描引擎: " + (File.Exists(Path.Combine(appBaseDir, "MYLScanEngine.exe")) ? "OK" : "MISSING"));
            sb.AppendLine("前端入口: " + (File.Exists(Path.Combine(appBaseDir, "web", "index.html")) ? "OK" : "MISSING"));
            sb.AppendLine("数据文件: " + (File.Exists(appDataPath) ? "OK" : "MISSING"));
            sb.AppendLine("默认模式: " + GetDefaultMode());
            sb.AppendLine("最大扫描文件数: " + GetRuntimeInt("maxFiles", 3000));
            sb.AppendLine("隔离保留天数: " + GetRuntimeInt("quarantineDays", 7));
            sb.AppendLine("运行日志: " + (GetRuntimeBool("logging", true) ? "ON" : "OFF"));
            sb.AppendLine("报告目录: " + Path.Combine(appBaseDir, "sdc-data", "reports"));
            sb.AppendLine("隔离目录: " + Path.Combine(appBaseDir, "sdc-data", "quarantine"));
            LogEvent("运行自检", "完成");
            return sb.ToString();
        }

        private string BuildResultMessage(CleanResult result, string report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("成功：" + result.Moved + " 项");
            sb.AppendLine("已失效/重复：" + result.Stale + " 项");
            sb.AppendLine("失败/跳过：" + result.Failed + " 项");
            sb.AppendLine("处理空间：" + FormatBytes(result.Bytes));
            sb.AppendLine();
            if (result.Moved == 0 && result.Stale > 0 && result.Failed == 0)
            {
                sb.AppendLine("说明：");
                sb.AppendLine("本次选择的候选文件已经不存在，通常是临时目录被系统或原软件自动清理了。工具已在下次刷新时过滤这些失效候选。");
                sb.AppendLine();
            }
            if (result.Failed > 0)
            {
                sb.AppendLine("失败原因示例：");
                foreach (var item in result.Details.Where(d => d.Status != "已隔离" && d.Status != "已恢复").Take(5))
                {
                    sb.AppendLine("- " + Path.GetFileName(item.Path) + "：" + item.Reason);
                }
                sb.AppendLine();
            }
            sb.AppendLine("报告已生成：");
            sb.AppendLine(report);
            return sb.ToString();
        }

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return string.Format("{0:N2} GB", bytes / (1024.0 * 1024.0 * 1024.0));
            if (bytes >= 1024L * 1024L) return string.Format("{0:N2} MB", bytes / (1024.0 * 1024.0));
            if (bytes >= 1024L) return string.Format("{0:N2} KB", bytes / 1024.0);
            return bytes + " B";
        }

        private void RestartAsAdministrator()
        {
            try
            {
                if (IsAdministrator())
                {
                    PostToast("当前已经是管理员模式");
                    return;
                }
                var psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                Process.Start(psi);
                BeginInvoke(new Action(Close));
            }
            catch (Exception ex)
            {
                PostToast("管理员启动失败：" + ex.Message.Replace("\"", "'"));
            }
        }

        private bool IsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private void OpenExistingPath(string path, bool folder)
        {
            if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                PostToast("路径不存在");
                return;
            }
            if (folder && File.Exists(path)) path = Path.GetDirectoryName(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        private void ShowSpecialAction(string key, string command)
        {
            string title = "专项清理说明";
            string body = "建议命令：\n" + command + "\n\n";
            if (key == "pip")
            {
                body += "说明：清理 Python 包下载缓存。清理后不影响已安装包，但以后安装相同包需要重新下载。";
            }
            else if (key == "npm")
            {
                body += "说明：清理 npm 下载缓存。可能导致下次安装依赖重新下载。";
            }
            else if (key == "docker")
            {
                body += "说明：Docker prune 可能删除未使用镜像、容器、网络或构建缓存。执行前请确认没有需要保留的镜像。";
            }
            else if (key == "gradle")
            {
                body += "说明：Gradle 缓存清理后，下次构建会重新下载依赖，项目首次构建会变慢。";
            }
            else if (key == "jetbrains")
            {
                body += "说明：建议从 IDE 内清理缓存，不要直接删除配置目录。";
            }
            else if (key == "vscode")
            {
                body += "说明：VS Code 扩展和用户设置不能直接删除，只建议检查缓存和日志目录。";
            }
            else
            {
                body += "说明：建议使用对应软件自带的缓存清理入口。";
            }
            body += "\n\n工具不会自动执行该命令，请你确认后手动执行。";
            MessageBox.Show(body, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            PostToast("已显示专项清理说明");
        }

        private void AddWhitelist(string path, string reason)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("路径为空");
            string white = Path.Combine(appBaseDir, "sdc-data", "whitelist.tsv");
            EnsureWhitelistFile(white);
            string full;
            try { full = Path.GetFullPath(path); }
            catch { full = path; }
            string existing = File.ReadAllText(white, Encoding.UTF8);
            if (existing.IndexOf(full, StringComparison.OrdinalIgnoreCase) >= 0) return;
            string line = full + "\t" + (reason ?? "用户加入白名单").Replace("\t", " ") + "\t" + DateTime.Now.ToString("s");
            File.AppendAllText(white, line + Environment.NewLine, Encoding.UTF8);
        }

        private void EnsureWhitelistFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path)) File.WriteAllText(path, "Path\tReason\tCreatedAt" + Environment.NewLine, Encoding.UTF8);
        }

        private bool IsProtectedPath(string path)
        {
            string drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            string[] roots =
            {
                Path.Combine(drive + "\\", "Windows", "System32"),
                Path.Combine(drive + "\\", "Windows", "SysWOW64"),
                Path.Combine(drive + "\\", "Windows", "WinSxS"),
                Path.Combine(drive + "\\", "Windows", "servicing"),
                Path.Combine(drive + "\\", "Windows", "Boot"),
                Path.Combine(drive + "\\", "Recovery"),
                Path.Combine(drive + "\\", "System Volume Information"),
                Path.Combine(drive + "\\", "Program Files"),
                Path.Combine(drive + "\\", "Program Files (x86)")
            };
            return roots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsFileLocked(string path)
        {
            try
            {
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                return false;
            }
            catch { return true; }
        }

        private string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }

        private string NewQuarantineName(string path)
        {
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(path))).Replace("-", "").Substring(0, 16);
                return hash + "-" + SanitizeFileName(Path.GetFileName(path));
            }
        }

        private string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "item.bin";
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value;
        }

        private void PostToast(string message)
        {
            string escaped = message.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
            webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"toast\",\"message\":\"" + escaped + "\"}");
        }

        private void PostOperationProgress(string operation, string stage, int done, int total, CleanResult result, string path)
        {
            try
            {
                string json = "{\"type\":\"operationProgress\",\"operation\":\"" + EscapeJson(operation) +
                    "\",\"stage\":\"" + EscapeJson(stage) +
                    "\",\"done\":" + done +
                    ",\"total\":" + total +
                    ",\"moved\":" + result.Moved +
                    ",\"stale\":" + result.Stale +
                    ",\"failed\":" + result.Failed +
                    ",\"path\":\"" + EscapeJson(ShortPath(path)) + "\"}";
                if (InvokeRequired) BeginInvoke(new Action(() => webView.CoreWebView2.PostWebMessageAsJson(json)));
                else webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }

        private void PostOperationComplete(string title, CleanResult result, string report)
        {
            try
            {
                string json = "{\"type\":\"operationComplete\",\"title\":\"" + EscapeJson(title) +
                    "\",\"moved\":" + result.Moved +
                    ",\"stale\":" + result.Stale +
                    ",\"failed\":" + result.Failed +
                    ",\"bytes\":\"" + EscapeJson(FormatBytes(result.Bytes)) +
                    "\",\"report\":\"" + EscapeJson(report) + "\"}";
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }

        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        }

        private string ShortPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            return path.Length > 120 ? "..." + path.Substring(path.Length - 117) : path;
        }

        private void PostReload()
        {
            webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"reload\"}");
        }

        private async Task RefreshDataAsync(string baseDir, string dataPath, string mode)
        {
            string engine = Path.Combine(baseDir, "MYLScanEngine.exe");
            if (!File.Exists(engine))
            {
                loadingTitle.Text = "扫描引擎缺失";
                loadingSubtitle.Text = "未找到 MYLScanEngine.exe，主界面将使用现有数据或空数据。";
                loadingFile.Text = engine;
                return;
            }
            if (string.IsNullOrWhiteSpace(mode)) mode = GetDefaultMode();
            if (mode != "Quick" && mode != "SoftwareLeftover" && mode != "Deep") mode = "Quick";
            int maxFiles = GetRuntimeInt("maxFiles", mode == "Deep" ? 12000 : (mode == "SoftwareLeftover" ? 7000 : 3000));

            StartScanHud(mode);
            await Task.Run(() =>
            {
                try
                {
                    var psi = new ProcessStartInfo();
                    psi.FileName = engine;
                    psi.Arguments = "--export-json \"" + dataPath + "\" --mode " + mode + " --max-files " + maxFiles;
                    psi.WorkingDirectory = baseDir;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    using (var p = Process.Start(psi))
                    {
                        if (p == null) return;
                        if (!p.WaitForExit(180000))
                        {
                            try { p.Kill(); } catch { }
                        }
                    }
                }
                catch
                {
                    // Keep existing data.json if refresh fails.
                }
            });
            LogEvent("扫描完成", "mode=" + mode + ", maxFiles=" + maxFiles);
            PostProcessDataJson(dataPath, mode);
            CompleteScanHud();
        }

        private void PostProcessDataJson(string dataPath, string mode)
        {
            if (!File.Exists(dataPath)) return;
            try
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                var root = serializer.DeserializeObject(File.ReadAllText(dataPath, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null) return;

                Dictionary<string, object> meta = root.ContainsKey("meta") ? root["meta"] as Dictionary<string, object> : null;
                if (meta == null)
                {
                    meta = new Dictionary<string, object>();
                    root["meta"] = meta;
                }
                meta["mode"] = ModeLabel(mode);
                meta["modeKey"] = mode;
                meta["lastScan"] = DateTime.Now.ToString("HH:mm");
                root["settings"] = BuildSettingsSnapshot();

                int removedMissing = FilterExistingCleanup(root);
                meta["filteredMissingFiles"] = removedMissing;
                meta["cleanupCandidates"] = root.ContainsKey("cleanup") ? ToArrayList(root["cleanup"]).Count : 0;

                if (mode == "SoftwareLeftover") EmphasizeSoftwareLeftover(root);
                if (mode == "Deep") EmphasizeDeepMode(root);

                root["quarantine"] = BuildQuarantineRows();
                root["reports"] = BuildReportRows();

                File.WriteAllText(dataPath, serializer.Serialize(root), new UTF8Encoding(false));
            }
            catch
            {
                // Keep original scanner output if post processing fails.
            }
        }

        private void RefreshDataAfterFileOperation()
        {
            if (!File.Exists(appDataPath)) return;
            try
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = int.MaxValue;
                var root = serializer.DeserializeObject(File.ReadAllText(appDataPath, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null) return;

                Dictionary<string, object> meta = root.ContainsKey("meta") ? root["meta"] as Dictionary<string, object> : null;
                if (meta == null)
                {
                    meta = new Dictionary<string, object>();
                    root["meta"] = meta;
                }
                int removedMissing = FilterExistingCleanup(root);
                meta["filteredMissingFiles"] = removedMissing;
                meta["cleanupCandidates"] = root.ContainsKey("cleanup") ? ToArrayList(root["cleanup"]).Count : 0;
                meta["lastOperation"] = DateTime.Now.ToString("HH:mm:ss");
                root["settings"] = BuildSettingsSnapshot();
                root["quarantine"] = BuildQuarantineRows();
                root["reports"] = BuildReportRows();

                File.WriteAllText(appDataPath, serializer.Serialize(root), new UTF8Encoding(false));
                LogEvent("轻量刷新", "清理/恢复后更新隔离区、报告和候选列表");
            }
            catch (Exception ex)
            {
                LogEvent("轻量刷新失败", ex.Message);
            }
        }

        private string ModeLabel(string mode)
        {
            if (mode == "Deep") return "深度诊断";
            if (mode == "SoftwareLeftover") return "软件残留扫描";
            return "快速扫描";
        }

        private int FilterExistingCleanup(Dictionary<string, object> root)
        {
            if (!root.ContainsKey("cleanup")) return 0;
            var rows = ToArrayList(root["cleanup"]);
            if (rows.Count == 0) return 0;
            var keep = new ArrayList();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int removed = 0;
            foreach (object item in rows)
            {
                var row = item as Dictionary<string, object>;
                if (row == null) { removed++; continue; }
                string path = row.ContainsKey("path") ? Convert.ToString(row["path"]) : "";
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !seen.Add(path))
                {
                    removed++;
                    continue;
                }
                keep.Add(row);
            }
            root["cleanup"] = keep;

            if (root.ContainsKey("metrics"))
            {
                var metrics = ToArrayList(root["metrics"]);
                if (metrics != null && metrics.Count > 1)
                {
                    long bytes = 0;
                    foreach (object item in keep)
                    {
                        var row = item as Dictionary<string, object>;
                        if (row != null && row.ContainsKey("size")) bytes += ParseSizeText(Convert.ToString(row["size"]));
                    }
                    var metric = metrics[1] as Dictionary<string, object>;
                    if (metric != null)
                    {
                        metric["value"] = FormatBytes(bytes);
                        metric["hint"] = removed > 0 ? "已过滤 " + removed + " 个失效临时文件候选" : "低风险候选，可先隔离";
                    }
                }
            }
            return removed;
        }

        private Dictionary<string, object> BuildSettingsSnapshot()
        {
            var snapshot = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                { "defaultMode", GetDefaultMode() },
                { "autoScan", GetRuntimeBool("autoScan", true) },
                { "quarantineDays", GetRuntimeInt("quarantineDays", 7) },
                { "maxFiles", GetRuntimeInt("maxFiles", 3000) },
                { "browserCache", GetRuntimeBool("browserCache", true) },
                { "developerCache", GetRuntimeBool("developerCache", true) },
                { "logging", GetRuntimeBool("logging", true) },
                { "advancedEvidence", GetRuntimeBool("advancedEvidence", true) }
            };
            foreach (var pair in runtimeSettings) snapshot[pair.Key] = pair.Value;
            return snapshot;
        }

        private void EmphasizeSoftwareLeftover(Dictionary<string, object> root)
        {
            if (!root.ContainsKey("cleanup")) return;
            var rows = ToArrayList(root["cleanup"]);
            var prioritized = new ArrayList();
            foreach (object item in rows)
            {
                var row = item as Dictionary<string, object>;
                if (row == null) continue;
                string path = Convert.ToString(row.ContainsKey("path") ? row["path"] : "");
                bool softwareRelated =
                    path.IndexOf("\\AppData\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\ProgramData\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\Package Cache\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\Cache\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\Temp\\", StringComparison.OrdinalIgnoreCase) >= 0;
                if (softwareRelated)
                {
                    row["reason"] = Convert.ToString(row.ContainsKey("reason") ? row["reason"] : "") + "；软件残留模式优先复核";
                    prioritized.Add(row);
                }
            }
            if (prioritized.Count > 0) root["cleanup"] = prioritized;
        }

        private void EmphasizeDeepMode(Dictionary<string, object> root)
        {
            if (root.ContainsKey("migration"))
            {
                foreach (object item in ToArrayList(root["migration"]))
                {
                    var row = item as Dictionary<string, object>;
                    if (row == null) continue;
                    string detail = Convert.ToString(row.ContainsKey("detail") ? row["detail"] : "");
                    if (detail.IndexOf("完整快照", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        row["detail"] = detail + " 深度诊断只给迁移/官方/禁止建议，迁移前建议完成完整快照和软件验证。";
                    }
                }
            }
            if (root.ContainsKey("cleanup"))
            {
                foreach (object item in ToArrayList(root["cleanup"]))
                {
                    var row = item as Dictionary<string, object>;
                    if (row == null) continue;
                    row["reason"] = Convert.ToString(row.ContainsKey("reason") ? row["reason"] : "") + "；深度模式仍按低风险隔离策略执行";
                }
            }
        }

        private ArrayList ToArrayList(object value)
        {
            var result = new ArrayList();
            if (value == null) return result;
            var arrayList = value as ArrayList;
            if (arrayList != null) return arrayList;
            var objectArray = value as object[];
            if (objectArray != null)
            {
                foreach (object item in objectArray) result.Add(item);
                return result;
            }
            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                foreach (object item in enumerable) result.Add(item);
            }
            return result;
        }

        private ArrayList BuildQuarantineRows()
        {
            var result = new ArrayList();
            string manifest = Path.Combine(appBaseDir, "sdc-data", "quarantine", "manifest.txt");
            if (!File.Exists(manifest)) return result;

            var groups = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(manifest, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] p = line.Split('\t');
                if (p.Length < 11) continue;
                string id = p[0];
                string original = p[1];
                string quarantined = p[2];
                long size;
                long.TryParse(p[3], out size);
                string source = p[4];
                string time = p[6];
                string expires = p[7];
                string batch = p[10];
                if (!groups.ContainsKey(batch))
                {
                    var row = new Dictionary<string, object>();
                    row["batch"] = batch;
                    row["count"] = 0;
                    row["source"] = source;
                    row["sizeBytes"] = 0L;
                    row["size"] = "0 B";
                    row["time"] = time;
                    row["expires"] = expires;
                    row["status"] = IsExpired(expires) ? "已过期" : "可恢复";
                    row["ids"] = new ArrayList();
                    row["paths"] = new ArrayList();
                    row["quarantinePaths"] = new ArrayList();
                    groups[batch] = row;
                }
                var g = groups[batch];
                g["count"] = Convert.ToInt32(g["count"]) + 1;
                long total = Convert.ToInt64(g["sizeBytes"]) + size;
                g["sizeBytes"] = total;
                g["size"] = FormatBytes(total);
                ((ArrayList)g["ids"]).Add(id);
                ((ArrayList)g["paths"]).Add(original);
                ((ArrayList)g["quarantinePaths"]).Add(quarantined);
            }
            foreach (var row in groups.Values.OrderByDescending(r => Convert.ToString(r["time"])))
            {
                result.Add(row);
            }
            return result;
        }

        private ArrayList BuildReportRows()
        {
            var result = new ArrayList();
            string reports = Path.Combine(appBaseDir, "sdc-data", "reports");
            if (!Directory.Exists(reports)) return result;
            foreach (FileInfo file in new DirectoryInfo(reports).GetFiles("*.txt").OrderByDescending(f => f.LastWriteTime).Take(20))
            {
                var row = new Dictionary<string, object>();
                row["name"] = file.Name;
                row["time"] = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                row["size"] = FormatBytes(file.Length);
                row["path"] = file.FullName;
                result.Add(row);
            }
            return result;
        }

        private bool IsExpired(string text)
        {
            DateTime value;
            return DateTime.TryParse(text, out value) && value < DateTime.Now;
        }

        private long ParseSizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            Match m = Regex.Match(value.Replace(",", ""), @"([\d.]+)\s*(B|KB|MB|GB|TB)", RegexOptions.IgnoreCase);
            if (!m.Success) return 0;
            double n;
            if (!double.TryParse(m.Groups[1].Value, out n)) return 0;
            string unit = m.Groups[2].Value.ToUpperInvariant();
            if (unit == "TB") n *= 1024D * 1024D * 1024D * 1024D;
            else if (unit == "GB") n *= 1024D * 1024D * 1024D;
            else if (unit == "MB") n *= 1024D * 1024D;
            else if (unit == "KB") n *= 1024D;
            return (long)n;
        }
    }

    public class HudProgressBar : Control
    {
        private int progressValue;

        public int Value
        {
            get { return progressValue; }
            set
            {
                progressValue = Math.Max(0, Math.Min(100, value));
                Invalidate();
            }
        }

        public HudProgressBar()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle track = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var trackBrush = new SolidBrush(Color.FromArgb(226, 233, 243)))
            {
                e.Graphics.FillRoundedRectangle(trackBrush, track, Height / 2);
            }

            int fillWidth = Math.Max(Height, (int)((Width - 1) * (progressValue / 100.0)));
            Rectangle fill = new Rectangle(0, 0, fillWidth, Height - 1);
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(fill, Color.FromArgb(24, 119, 242), Color.FromArgb(35, 201, 143), 0F))
            {
                e.Graphics.FillRoundedRectangle(brush, fill, Height / 2);
            }
        }
    }

    static class GraphicsExtensions
    {
        public static void FillRoundedRectangle(this Graphics graphics, Brush brush, Rectangle bounds, int radius)
        {
            using (var path = RoundedPath(bounds, radius))
            {
                graphics.FillPath(brush, path);
            }
        }

        public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle bounds, int radius)
        {
            using (var path = RoundedPath(bounds, radius))
            {
                graphics.DrawPath(pen, path);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle bounds, int radius)
        {
            int diameter = Math.Max(1, radius * 2);
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && Array.Exists(args, a => string.Equals(a, "--self-check", StringComparison.OrdinalIgnoreCase)))
            {
                string report = SelfCheck.Run();
                try
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sdc-data", "reports");
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "self-check-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"), report, new UTF8Encoding(false));
                }
                catch { }
                Console.WriteLine(report);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow());
        }
    }

    public class CleanResult
    {
        public int Moved;
        public int Failed;
        public int Stale;
        public long Bytes;
        public string BatchId;
        public string ReportPath;
        public List<CleanDetail> Details = new List<CleanDetail>();
    }

    public class CleanDetail
    {
        public string Status;
        public string Path;
        public string Reason;
    }

    static class SelfCheck
    {
        public static string Run()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string web = Path.Combine(baseDir, "web");
            string data = Path.Combine(web, "data.json");
            string engine = Path.Combine(baseDir, "MYLScanEngine.exe");
            string probe = Path.Combine(web, "self-check-data.json");
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("SELF_CHECK_START");
            lines.AppendLine("WebExists=" + Directory.Exists(web));
            lines.AppendLine("IndexExists=" + File.Exists(Path.Combine(web, "index.html")));
            lines.AppendLine("AppExists=" + File.Exists(Path.Combine(web, "app.js")));
            lines.AppendLine("DataExists=" + File.Exists(data));
            lines.AppendLine("EngineExists=" + File.Exists(engine));
            if (File.Exists(data))
            {
                string text = File.ReadAllText(data);
                lines.AppendLine("DataHasRealSource=" + text.Contains("\"source\": \"真实扫描\""));
                lines.AppendLine("DataHasMigrationPath=" + text.Contains("\"path\""));
                lines.AppendLine("DataLength=" + text.Length);
            }
            if (File.Exists(engine))
            {
                try
                {
                    var psi = new ProcessStartInfo();
                    psi.FileName = engine;
                    psi.Arguments = "--export-json \"" + probe + "\" --mode Quick --max-files 200";
                    psi.WorkingDirectory = baseDir;
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    using (var p = Process.Start(psi))
                    {
                        bool exited = p.WaitForExit(15000);
                        if (!exited)
                        {
                            try { p.Kill(); } catch { }
                        }
                        lines.AppendLine("EngineExited=" + exited);
                        lines.AppendLine("EngineExitCode=" + (exited ? p.ExitCode.ToString() : "TIMEOUT"));
                    }
                    lines.AppendLine("ProbeExists=" + File.Exists(probe));
                    if (File.Exists(probe))
                    {
                        string probeText = File.ReadAllText(probe);
                        lines.AppendLine("ProbeHasDiagnosis=" + probeText.Contains("\"diagnosis\""));
                        lines.AppendLine("ProbeHasMigrationTarget=" + probeText.Contains("\"target\""));
                        try { File.Delete(probe); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    lines.AppendLine("EngineError=" + ex.Message);
                }
            }
            lines.AppendLine("SELF_CHECK_END");
            return lines.ToString();
        }
    }
}

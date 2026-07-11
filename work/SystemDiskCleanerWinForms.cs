using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SystemDiskCleaner
{
    public class FileFinding
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public string SizeText { get { return FormatBytes(Size); } }
        public string Category { get; set; }
        public string Source { get; set; }
        public string Risk { get; set; }
        public string RecommendedAction { get; set; }
        public string Reason { get; set; }
        public DateTime LastWriteTime { get; set; }
        public bool Locked { get; set; }
        public bool Recoverable { get; set; }
        public string SignatureStatus { get; set; }
        public string Publisher { get; set; }
        public string OwnerSoftware { get; set; }
        public string ServiceReference { get; set; }
        public string SafetyNotes { get; set; }
        public int RiskScore { get; set; }

        public static string FormatBytes(double bytes)
        {
            if (bytes >= 1024 * 1024 * 1024) return string.Format("{0:N2} GB", bytes / (1024 * 1024 * 1024));
            if (bytes >= 1024 * 1024) return string.Format("{0:N2} MB", bytes / (1024 * 1024));
            if (bytes >= 1024) return string.Format("{0:N2} KB", bytes / 1024);
            return string.Format("{0:N0} B", bytes);
        }
    }

    public class ScanTarget
    {
        public string Path;
        public string Source;
        public string Category;
        public string Risk;
        public string Action;
        public string Reason;
    }

    public class QuarantineItem
    {
        public string Id;
        public string BatchId;
        public string OriginalPath;
        public string QuarantinePath;
        public long Size;
        public string Source;
        public string Risk;
        public DateTime CleanedAt;
        public DateTime ExpiresAt;
        public string Sha256;
        public string SnapshotReason;
    }

    public class InstalledSoftware
    {
        public string Name;
        public string Publisher;
        public string InstallLocation;
        public string UninstallString;
    }

    public class ServiceReference
    {
        public string Name;
        public string DisplayName;
        public string PathName;
    }

    public class SpaceDiagnosticItem
    {
        public string Area;
        public string Path;
        public long Size;
        public string SizeText { get { return FileFinding.FormatBytes(Size); } }
        public string Recommendation;
        public string ActionType;
        public string Risk;
        public string Reason;
        public string SpecialKey;
        public string SpecialCommand;
        public string RiskExplanation;
    }

    public class CleanerRule
    {
        public string Name;
        public string PathContains;
        public string Category;
        public string Risk;
        public string Action;
        public string Recommendation;
        public string Reason;
    }

    public class MainForm : Form
    {
        private readonly string dataRoot;
        private readonly string reportRoot;
        private readonly string quarantineRoot;
        private readonly string manifestPath;
        private readonly string rulesPath;
        private readonly string whitelistPath;
        private readonly List<FileFinding> findings = new List<FileFinding>();
        private readonly DataGridView grid = new DataGridView();
        private readonly ComboBox modeBox = new ComboBox();
        private readonly NumericUpDown maxFilesBox = new NumericUpDown();
        private readonly Button overviewButton = new Button();
        private readonly Button scanButton = new Button();
        private readonly Button diagnoseButton = new Button();
        private readonly Button cleanButton = new Button();
        private readonly Button quarantineButton = new Button();
        private readonly Button exportButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly Label summaryLabel = new Label();
        private readonly TextBox detailBox = new TextBox();
        private readonly ProgressBar progressBar = new ProgressBar();
        private int skippedDirectories;
        private List<InstalledSoftware> installedSoftware = new List<InstalledSoftware>();
        private List<ServiceReference> serviceReferences = new List<ServiceReference>();
        private List<CleanerRule> cleanerRules = new List<CleanerRule>();

        public string DataRoot { get { return dataRoot; } }
        public string ReportRoot { get { return reportRoot; } }
        public string QuarantineRoot { get { return quarantineRoot; } }
        public int FindingCount { get { return findings.Count; } }

        public MainForm()
        {
            Text = "系统盘清理工具";
            MinimumSize = new Size(1040, 680);
            Size = new Size(1180, 760);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            dataRoot = Path.Combine(baseDir, "sdc-data");
            reportRoot = Path.Combine(dataRoot, "reports");
            quarantineRoot = Path.Combine(dataRoot, "quarantine");
            manifestPath = Path.Combine(quarantineRoot, "manifest.txt");
            rulesPath = Path.Combine(dataRoot, "rules.tsv");
            whitelistPath = Path.Combine(dataRoot, "whitelist.tsv");
            Directory.CreateDirectory(reportRoot);
            Directory.CreateDirectory(quarantineRoot);
            EnsureDefaultRules();
            EnsureWhitelist();
            cleanerRules = LoadCleanerRules();

            BuildUi();
        }

        private void BuildUi()
        {
            var shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.ColumnCount = 2;
            shell.RowCount = 1;
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 178));
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.BackColor = Color.FromArgb(242, 245, 249);
            Controls.Add(shell);

            var sidebar = new Panel();
            sidebar.Dock = DockStyle.Fill;
            sidebar.BackColor = Color.FromArgb(18, 28, 45);
            sidebar.Padding = new Padding(14, 16, 14, 14);
            shell.Controls.Add(sidebar, 0, 0);

            var navTitle = new Label
            {
                Text = "SDC",
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(16, 18)
            };
            var navSub = new Label
            {
                Text = "空间治理",
                ForeColor = Color.FromArgb(151, 164, 184),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 58)
            };
            sidebar.Controls.Add(navTitle);
            sidebar.Controls.Add(navSub);

            int navTop = 104;
            sidebar.Controls.Add(CreateNavButton("空间总览", navTop, async (s, e) => await StartOverviewAsync(), true));
            sidebar.Controls.Add(CreateNavButton("空间诊断", navTop + 44, async (s, e) => await StartSpaceDiagnosisAsync(), false));
            sidebar.Controls.Add(CreateNavButton("安全扫描", navTop + 88, async (s, e) => await StartScanAsync(), false));
            sidebar.Controls.Add(CreateNavButton("隔离区", navTop + 132, (s, e) => ShowQuarantine(), false));
            sidebar.Controls.Add(CreateNavButton("导出报告", navTop + 176, (s, e) => ExportCsv(), false));

            var navHint = new Label
            {
                Text = "建议流程：\n1. 空间总览\n2. 空间诊断\n3. 官方清理/迁移\n4. 安全扫描",
                ForeColor = Color.FromArgb(151, 164, 184),
                Font = new Font("Microsoft YaHei UI", 8.5F),
                AutoSize = false,
                Size = new Size(142, 120),
                Location = new Point(18, 350)
            };
            sidebar.Controls.Add(navHint);

            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 4;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
            root.BackColor = Color.FromArgb(242, 245, 249);
            shell.Controls.Add(root, 1, 0);

            var top = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 14, 22, 8), BackColor = Color.FromArgb(248, 250, 252) };
            var title = new Label { Text = "C 盘空间治理中心", ForeColor = Color.FromArgb(24, 36, 56), Font = new Font("Microsoft YaHei UI", 18F, FontStyle.Bold), AutoSize = true, Location = new Point(22, 14) };
            var subtitle = new Label { Text = "扫得更深，删得更少：优先诊断、迁移和官方清理，低风险项目才进入隔离清理。", ForeColor = Color.FromArgb(96, 108, 128), Font = new Font("Microsoft YaHei UI", 9.5F), AutoSize = true, Location = new Point(24, 52) };
            statusLabel.Text = "就绪";
            statusLabel.ForeColor = Color.FromArgb(28, 126, 70);
            statusLabel.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(24, 78);
            var badge = new Label
            {
                Text = "保守模式",
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(84, 26),
                Location = new Point(250, 17),
                BackColor = Color.FromArgb(225, 242, 233),
                ForeColor = Color.FromArgb(28, 126, 70),
                Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold)
            };
            top.Controls.Add(title);
            top.Controls.Add(subtitle);
            top.Controls.Add(statusLabel);
            top.Controls.Add(badge);
            root.Controls.Add(top, 0, 0);

            var toolbar = new FlowLayoutPanel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.Padding = new Padding(18, 10, 18, 8);
            toolbar.BackColor = Color.White;
            toolbar.WrapContents = false;
            root.Controls.Add(toolbar, 0, 1);

            toolbar.Controls.Add(new Label { Text = "扫描模式", AutoSize = true, ForeColor = Color.FromArgb(78, 88, 105), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold), Padding = new Padding(0, 8, 4, 0) });
            modeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modeBox.Items.AddRange(new object[] { "快速扫描", "软件残留", "深度扫描" });
            modeBox.SelectedIndex = 0;
            modeBox.Width = 150;
            toolbar.Controls.Add(modeBox);

            toolbar.Controls.Add(new Label { Text = "最多文件数", AutoSize = true, ForeColor = Color.FromArgb(78, 88, 105), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold), Padding = new Padding(16, 8, 4, 0) });
            maxFilesBox.Minimum = 100;
            maxFilesBox.Maximum = 1000000;
            maxFilesBox.Value = 5000;
            maxFilesBox.Increment = 1000;
            maxFilesBox.Width = 90;
            toolbar.Controls.Add(maxFilesBox);

            overviewButton.Text = "空间总览";
            overviewButton.Width = 110;
            StylePrimaryButton(overviewButton);
            overviewButton.Click += async (s, e) => await StartOverviewAsync();
            toolbar.Controls.Add(overviewButton);

            scanButton.Text = "开始扫描";
            scanButton.Width = 110;
            StylePrimaryButton(scanButton);
            scanButton.Click += async (s, e) => await StartScanAsync();
            toolbar.Controls.Add(scanButton);

            diagnoseButton.Text = "空间诊断";
            diagnoseButton.Width = 110;
            StyleSecondaryButton(diagnoseButton);
            diagnoseButton.Click += async (s, e) => await StartSpaceDiagnosisAsync();
            toolbar.Controls.Add(diagnoseButton);

            cleanButton.Text = "清理选中项";
            cleanButton.Width = 120;
            cleanButton.Enabled = false;
            StyleDangerButton(cleanButton);
            cleanButton.Click += (s, e) => CleanSelected();
            toolbar.Controls.Add(cleanButton);

            quarantineButton.Text = "隔离区";
            quarantineButton.Width = 100;
            StyleSecondaryButton(quarantineButton);
            quarantineButton.Click += (s, e) => ShowQuarantine();
            toolbar.Controls.Add(quarantineButton);

            exportButton.Text = "导出报告";
            exportButton.Width = 100;
            exportButton.Enabled = false;
            StyleSecondaryButton(exportButton);
            exportButton.Click += (s, e) => ExportCsv();
            toolbar.Controls.Add(exportButton);

            progressBar.Width = 180;
            progressBar.Height = 23;
            progressBar.Style = ProgressBarStyle.Continuous;
            toolbar.Controls.Add(progressBar);

            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = true;
            grid.RowHeadersVisible = false;
            grid.BackgroundColor = Color.FromArgb(248, 250, 252);
            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(232, 238, 246);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(31, 42, 55);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(209, 228, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(25, 35, 50);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 255);
            grid.CellFormatting += Grid_CellFormatting;
            grid.SelectionChanged += (s, e) => UpdateDetails();
            root.Controls.Add(grid, 0, 2);

            var bottom = new TableLayoutPanel();
            bottom.Dock = DockStyle.Fill;
            bottom.ColumnCount = 2;
            bottom.RowCount = 1;
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            bottom.Padding = new Padding(18, 10, 18, 12);
            bottom.BackColor = Color.FromArgb(242, 245, 249);
            root.Controls.Add(bottom, 0, 3);

            summaryLabel.Dock = DockStyle.Fill;
            summaryLabel.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            summaryLabel.ForeColor = Color.FromArgb(34, 50, 70);
            summaryLabel.Text = "可安全清理：0 B    需要确认：0 B    建议保留：0 B    系统保护：0 B";
            bottom.Controls.Add(summaryLabel, 0, 0);

            detailBox.Dock = DockStyle.Fill;
            detailBox.Multiline = true;
            detailBox.ReadOnly = true;
            detailBox.ScrollBars = ScrollBars.Vertical;
            detailBox.BackColor = Color.White;
            detailBox.BorderStyle = BorderStyle.FixedSingle;
            detailBox.Font = new Font("Microsoft YaHei UI", 9F);
            detailBox.Text = "选择一行后，这里会显示路径、来源、风险和清理原因。";
            bottom.Controls.Add(detailBox, 1, 0);
        }

        private Button CreateNavButton(string text, int top, EventHandler click, bool selected)
        {
            var button = new Button();
            button.Text = text;
            button.Width = 146;
            button.Height = 36;
            button.Left = 16;
            button.Top = top;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(12, 0, 0, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = selected ? Color.FromArgb(38, 111, 255) : Color.FromArgb(18, 28, 45);
            button.ForeColor = selected ? Color.White : Color.FromArgb(220, 228, 238);
            button.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            button.Click += click;
            return button;
        }

        private void StylePrimaryButton(Button button)
        {
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(38, 111, 255);
            button.ForeColor = Color.White;
            button.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            button.Margin = new Padding(8, 0, 0, 0);
        }

        private void StyleSecondaryButton(Button button)
        {
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(196, 205, 218);
            button.BackColor = Color.FromArgb(248, 250, 252);
            button.ForeColor = Color.FromArgb(45, 55, 72);
            button.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            button.Margin = new Padding(8, 0, 0, 0);
        }

        private void StyleDangerButton(Button button)
        {
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = Color.FromArgb(218, 72, 72);
            button.ForeColor = Color.White;
            button.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            button.Margin = new Padding(8, 0, 0, 0);
        }

        private async Task StartScanAsync()
        {
            findings.Clear();
            skippedDirectories = 0;
            BindGrid();
            SetBusy(true);
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Text = "正在扫描，可能需要一些时间...";

            string mode = GetSelectedModeKey();
            int limit = (int)maxFilesBox.Value;

            try
            {
                installedSoftware = LoadInstalledSoftware();
                serviceReferences = LoadServiceReferences();
                var result = await Task.Run(() => Scan(mode, limit));
                findings.Clear();
                findings.AddRange(result);
                BindGrid();
                UpdateSummary();
                statusLabel.Text = string.Format("扫描完成，已分析 {0:N0} 个文件，跳过 {1:N0} 个无权限或不可访问目录。", findings.Count, skippedDirectories);
                cleanButton.Enabled = findings.Any(f => f.RecommendedAction == "Clean" && f.Risk == "Low");
                exportButton.Enabled = findings.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "扫描失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "扫描失败。";
            }
            finally
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 0;
                SetBusy(false);
            }
        }

        private async Task StartSpaceDiagnosisAsync()
        {
            SetBusy(true);
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Text = "正在进行空间诊断，只分析占用，不删除文件...";

            try
            {
                var items = await Task.Run(() => RunSpaceDiagnosis());
                ShowSpaceDiagnosis(items);
                statusLabel.Text = string.Format("空间诊断完成，发现 {0:N0} 个重点占用项。", items.Count);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "空间诊断失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "空间诊断失败。";
            }
            finally
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 0;
                SetBusy(false);
            }
        }

        private async Task StartOverviewAsync()
        {
            SetBusy(true);
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Text = "正在生成空间总览...";

            try
            {
                var items = await Task.Run(() => RunSpaceDiagnosis());
                ShowSpaceOverview(items);
                statusLabel.Text = "空间总览已生成。";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "空间总览失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "空间总览失败。";
            }
            finally
            {
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 0;
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            overviewButton.Enabled = !busy;
            scanButton.Enabled = !busy;
            diagnoseButton.Enabled = !busy;
            quarantineButton.Enabled = !busy;
            exportButton.Enabled = !busy && findings.Count > 0;
            cleanButton.Enabled = !busy && findings.Any(f => f.RecommendedAction == "Clean" && f.Risk == "Low");
        }

        private List<SpaceDiagnosticItem> RunSpaceDiagnosis()
        {
            string drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            string root = drive + "\\";
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var items = new List<SpaceDiagnosticItem>();

            AddDiagnosticIfExists(items, "C 盘根目录", root, "先看总体占用", "诊断", "低", "用于判断 C 盘最大空间来源。");
            AddDiagnosticIfExists(items, "用户目录", user, "优先检查可迁移内容", "迁移", "低", "下载、桌面、文档、视频经常是真正的大户。");
            AddDiagnosticIfExists(items, "下载目录", Path.Combine(user, "Downloads"), "迁移或手动整理", "迁移", "低", "用户下载文件通常可以移动到其他磁盘。");
            AddDiagnosticIfExists(items, "桌面", Path.Combine(user, "Desktop"), "迁移或手动整理", "迁移", "低", "桌面文件是用户内容，不建议工具自动删除。");
            AddDiagnosticIfExists(items, "文档", Path.Combine(user, "Documents"), "迁移或手动整理", "迁移", "低", "文档是用户内容，只建议迁移。");
            AddDiagnosticIfExists(items, "视频", Path.Combine(user, "Videos"), "迁移", "迁移", "低", "视频文件通常占用较大，适合迁移到数据盘。");
            AddDiagnosticIfExists(items, "本地 AppData", local, "按软件来源分析", "诊断", "中", "这里包含缓存、配置、数据库，不能一刀切删除。");
            AddDiagnosticIfExists(items, "漫游 AppData", roaming, "建议保留，仅做专项分析", "保留", "高", "这里常含配置、聊天数据、浏览器 Profile、存档。");
            AddDiagnosticIfExists(items, "ProgramData", Path.Combine(root, "ProgramData"), "按软件来源分析", "诊断", "中", "共享软件数据，需要识别归属后处理。");
            AddDiagnosticIfExists(items, "Program Files", Path.Combine(root, "Program Files"), "卸载软件，不手动删除", "卸载", "高", "安装目录不建议手动清理。");
            AddDiagnosticIfExists(items, "Program Files (x86)", Path.Combine(root, "Program Files (x86)"), "卸载软件，不手动删除", "卸载", "高", "安装目录不建议手动清理。");
            AddDiagnosticIfExists(items, "Windows 目录", Path.Combine(root, "Windows"), "使用系统官方清理", "官方清理", "高", "Windows 目录不能靠手动删除释放空间。");
            AddDiagnosticIfExists(items, "Windows 更新缓存", Path.Combine(root, "Windows", "SoftwareDistribution", "Download"), "使用系统更新清理或停止服务后专项清理", "官方清理", "中", "Windows 更新下载缓存，建议走官方清理入口。");
            AddDiagnosticIfExists(items, "WinSxS", Path.Combine(root, "Windows", "WinSxS"), "只能用 DISM 组件清理", "官方清理", "禁止", "组件存储不能手动删除。");
            AddDiagnosticIfExists(items, "Windows Installer", Path.Combine(root, "Windows", "Installer"), "禁止手动删除", "禁止", "禁止", "安装包缓存影响软件修复、卸载和更新。");
            AddDiagnosticIfExists(items, "回收站", Path.Combine(root, "$Recycle.Bin"), "可清空回收站", "清理", "低", "确认不需要恢复后可清理。");

            AddDiagnosticIfExists(items, "微信文件", Path.Combine(user, "Documents", "WeChat Files"), "迁移聊天文件位置", "迁移", "低", "聊天文件常占很大空间，但不应直接删除。");
            AddDiagnosticIfExists(items, "企业微信文件", Path.Combine(user, "Documents", "WXWork"), "迁移或在软件内清理", "迁移", "低", "企业微信文件建议通过软件设置或迁移处理。");
            AddDiagnosticIfExists(items, "QQ 文件", Path.Combine(user, "Documents", "Tencent Files"), "迁移聊天文件位置", "迁移", "低", "聊天文件不建议直接删除。");
            AddDiagnosticIfExists(items, "Docker 数据", Path.Combine(local, "Docker"), "使用 Docker prune 或迁移数据目录", "专项清理", "中", "容器镜像和构建缓存可能很大，需要专项命令处理。");
            AddDiagnosticIfExists(items, "WSL 数据", Path.Combine(local, "Packages"), "查找 Linux 发行版，可导出迁移", "迁移", "中", "WSL 发行版通常存放在 Packages 下，不能直接删除。");
            AddDiagnosticIfExists(items, "npm 缓存", Path.Combine(local, "npm-cache"), "可用 npm cache clean", "专项清理", "低", "开发缓存可通过官方命令清理。");
            AddDiagnosticIfExists(items, "pip 缓存", Path.Combine(local, "pip", "Cache"), "可用 pip cache purge", "专项清理", "低", "Python 包缓存可通过官方命令清理。");
            AddDiagnosticIfExists(items, "Gradle 缓存", Path.Combine(user, ".gradle", "caches"), "可清旧缓存", "专项清理", "中", "构建缓存可清，但下次构建会重新下载。");
            AddDiagnosticIfExists(items, "Maven 仓库", Path.Combine(user, ".m2", "repository"), "谨慎清理或迁移", "迁移", "中", "依赖仓库可能很大，删除后需重新下载。");
            AddDiagnosticIfExists(items, "JetBrains 缓存", Path.Combine(local, "JetBrains"), "使用 IDE 缓存清理或迁移", "专项清理", "中", "IDE 缓存可清，但配置和索引需要区分。");
            AddDiagnosticIfExists(items, "VS Code 数据", Path.Combine(roaming, "Code"), "保留配置，仅清缓存", "专项清理", "中", "扩展和用户设置不应直接删除。");

            items.AddRange(GetLargestChildren("用户目录大户", user, 12, "迁移或手动整理", "迁移", "低", "用户内容建议迁移，不建议自动删除。"));
            items.AddRange(GetLargestChildren("AppData 大户", local, 12, "按软件来源分析", "诊断", "中", "AppData 既有缓存也有配置，需要进一步识别。"));
            items.AddRange(GetLargestChildren("ProgramData 大户", Path.Combine(root, "ProgramData"), 10, "按软件来源分析", "诊断", "中", "ProgramData 需要识别软件归属。"));

            return items
                .Where(i => i.Size > 0)
                .GroupBy(i => i.Path.ToLowerInvariant())
                .Select(g => g.OrderByDescending(x => x.Size).First())
                .OrderByDescending(i => i.Size)
                .Take(80)
                .ToList();
        }

        private void AddDiagnosticIfExists(List<SpaceDiagnosticItem> items, string area, string path, string recommendation, string action, string risk, string reason)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            CleanerRule rule = MatchRule(path);
            if (rule != null)
            {
                recommendation = rule.Recommendation;
                action = RuleRecommendationToAction(rule.Recommendation, action);
                risk = rule.Risk == "Blocked" ? "禁止" : ToZhRisk(rule.Risk);
                reason = rule.Reason;
                area = rule.Name;
            }
            items.Add(new SpaceDiagnosticItem
            {
                Area = area,
                Path = path,
                Size = GetDirectorySizeSafe(path, 60000),
                Recommendation = recommendation,
                ActionType = action,
                Risk = risk,
                Reason = reason,
                SpecialKey = GetSpecialKey(area, path, action),
                SpecialCommand = GetSpecialCommand(area, path, action),
                RiskExplanation = BuildRiskExplanation(action, risk, reason)
            });
        }

        private string GetSpecialKey(string area, string path, string action)
        {
            if (action != "专项清理") return "";
            string text = (area + " " + path).ToLowerInvariant();
            if (text.Contains("pip")) return "pip";
            if (text.Contains("npm")) return "npm";
            if (text.Contains("docker")) return "docker";
            if (text.Contains("gradle")) return "gradle";
            if (text.Contains("jetbrains")) return "jetbrains";
            if (text.Contains("code")) return "vscode";
            return "special";
        }

        private string GetSpecialCommand(string area, string path, string action)
        {
            string key = GetSpecialKey(area, path, action);
            if (key == "pip") return "pip cache purge";
            if (key == "npm") return "npm cache clean --force";
            if (key == "docker") return "docker system df && docker system prune";
            if (key == "gradle") return "gradle --stop 后手动检查 %USERPROFILE%\\.gradle\\caches";
            if (key == "jetbrains") return "在 JetBrains IDE 内执行 File > Invalidate Caches";
            if (key == "vscode") return "保留配置，仅检查 Cache、CachedData、logs 目录";
            return "使用对应软件的缓存清理入口";
        }

        private string BuildRiskExplanation(string action, string risk, string reason)
        {
            if (action == "清理") return "低风险候选只来自临时、缓存、日志或崩溃转储路径，执行前仍会二次检查路径、占用和权限，并先进入隔离区。";
            if (action == "迁移") return "这是用户数据或软件数据，不会自动删除。建议复制到目标盘、校验后修改软件路径，并保留原目录观察。";
            if (action == "官方清理") return "涉及 Windows 组件或系统缓存，只能通过 Windows 官方工具处理，禁止手动删除目录。";
            if (action == "专项清理") return "涉及开发工具、容器或 IDE 缓存，需要用对应软件命令处理；直接删除可能导致重新下载、索引重建或环境异常。";
            if (action == "禁止") return "该区域可能影响系统修复、卸载、启动或组件完整性，工具不会提供删除入口。";
            if (action == "卸载") return "这是软件安装目录，应通过控制面板或软件卸载器释放空间，不建议手动删除。";
            return reason;
        }

        private string RuleRecommendationToAction(string recommendation, string fallback)
        {
            if (string.IsNullOrWhiteSpace(recommendation)) return fallback;
            if (recommendation.Contains("官方")) return "官方清理";
            if (recommendation.Contains("专项")) return "专项清理";
            if (recommendation.Contains("迁移")) return "迁移";
            if (recommendation.Contains("禁止")) return "禁止";
            if (recommendation.Contains("保留")) return "保留";
            if (recommendation.Contains("清理")) return "清理";
            return fallback;
        }

        private List<SpaceDiagnosticItem> GetLargestChildren(string area, string root, int top, string recommendation, string action, string risk, string reason)
        {
            var result = new List<SpaceDiagnosticItem>();
            if (!Directory.Exists(root)) return result;
            string[] dirs = new string[0];
            try { dirs = Directory.GetDirectories(root); } catch { return result; }
            foreach (string dir in dirs)
            {
                if (IsProtectedPath(dir)) continue;
                long size = GetDirectorySizeSafe(dir, 30000);
                if (size <= 0) continue;
                result.Add(new SpaceDiagnosticItem
                {
                    Area = area,
                    Path = dir,
                    Size = size,
                    Recommendation = recommendation,
                    ActionType = action,
                    Risk = risk,
                    Reason = reason
                });
            }
            return result.OrderByDescending(i => i.Size).Take(top).ToList();
        }

        private long GetDirectorySizeSafe(string root, int maxFiles)
        {
            long total = 0;
            int count = 0;
            foreach (string file in EnumerateFilesSafe(root))
            {
                if (count >= maxFiles) break;
                try
                {
                    total += new FileInfo(file).Length;
                    count++;
                }
                catch { }
            }
            return total;
        }

        private string GetSelectedModeKey()
        {
            string text = modeBox.SelectedItem == null ? "快速扫描" : modeBox.SelectedItem.ToString();
            if (text == "软件残留") return "SoftwareLeftover";
            if (text == "深度扫描") return "Deep";
            return "Quick";
        }

        private string ToZhRisk(string value)
        {
            if (value == "Low") return "低风险";
            if (value == "Medium") return "中风险";
            if (value == "High") return "高风险";
            if (value == "Blocked") return "已保护";
            return value ?? "";
        }

        private string ToZhAction(string value)
        {
            if (value == "Clean") return "可清理";
            if (value == "Review") return "需确认";
            if (value == "Keep") return "保留";
            if (value == "Locked") return "已锁定";
            if (value == "Skip") return "跳过";
            return value ?? "";
        }

        private string ToZhCategory(string value)
        {
            if (value == "SoftwareGenerated") return "软件产生";
            if (value == "SystemTemp") return "系统临时";
            if (value == "SystemProtected") return "系统保护";
            if (value == "UserFile") return "用户文件";
            return value ?? "";
        }

        private string ToZhSignature(string status, string publisher)
        {
            if (status == "NotChecked") return "未检查";
            if (status == "UnsignedOrInvalid") return "未签名/无效";
            if (status == "Signed")
            {
                return string.IsNullOrWhiteSpace(publisher) ? "已签名" : "已签名：" + publisher;
            }
            return status ?? "";
        }

        private string ToZhSource(string value)
        {
            if (value == "User Temp") return "用户临时目录";
            if (value == "Windows Temp") return "Windows 临时目录";
            if (value == "Local Temp") return "本地临时目录";
            if (value == "Crash Dumps") return "崩溃转储";
            if (value == "Chrome") return "Chrome 浏览器";
            if (value == "Edge") return "Edge 浏览器";
            if (value == "Firefox") return "Firefox 浏览器";
            if (value == "Local AppData") return "本地 AppData";
            if (value == "Roaming AppData") return "漫游 AppData";
            if (value == "ProgramData") return "ProgramData";
            if (value == "Users") return "用户目录";
            if (value == "Program Files") return "程序安装目录";
            return value ?? "";
        }

        private string ToZhReason(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("Current user temporary files.", "当前用户临时文件。")
                .Replace("Windows temporary files, excluding protected locked files.", "Windows 临时文件，已排除受保护或被占用的文件。")
                .Replace("Per-user application temporary files.", "当前用户的软件临时文件。")
                .Replace("Application crash dumps.", "软件崩溃转储文件。")
                .Replace("Browser cache can be regenerated.", "浏览器缓存，可重新生成。")
                .Replace("Firefox profile tree may contain cache and user data.", "Firefox 配置目录可能同时包含缓存和用户数据，需要确认。")
                .Replace("Application-generated data. Review before cleanup.", "软件产生的数据，清理前需要确认。")
                .Replace("May contain app settings, profiles, saves, and databases.", "可能包含软件设置、用户配置、存档或数据库，建议保留。")
                .Replace("Shared application data. Review before cleanup.", "共享软件数据，清理前需要确认。")
                .Replace("User files are not automatically cleaned.", "用户文件不会自动清理。")
                .Replace("Installed application files are not cleanup candidates.", "已安装软件文件不作为清理候选。")
                .Replace("Protected Windows path.", "受保护的 Windows 系统路径。")
                .Replace("Common regenerable temporary, log, dump, or backup extension.", "常见临时、日志、转储或备份扩展名，通常可重新生成。")
                .Replace("Cache or temp path can usually be regenerated.", "缓存或临时目录内容通常可重新生成。")
                .Replace("User-owned content is not automatically cleaned.", "用户个人内容不会自动清理。")
                .Replace("File appears to be in use.", "文件似乎正在被占用。")
                .Replace("Installed application ownership matched. Keep by default.", "命中已安装软件归属，默认保留。")
                .Replace("Referenced by Windows service.", "被 Windows 服务引用，禁止清理。")
                .Replace("Microsoft signed file.", "Microsoft 签名文件，禁止清理。")
                .Replace("Risk score is high. Keep by default.", "风险评分较高，默认保留。")
                .Replace("Risk score requires manual review.", "风险评分需要人工确认。");
        }

        private List<FileFinding> Scan(string mode, int limit)
        {
            var list = new List<FileFinding>();
            int count = 0;
            foreach (var target in GetScanTargets(mode))
            {
                foreach (string path in EnumerateFilesSafe(target.Path))
                {
                    if (count >= limit) break;
                    try
                    {
                        var info = new FileInfo(path);
                        var finding = GetFindingForFile(info, target);
                        finding.Id = (count + 1).ToString("D6");
                        list.Add(finding);
                        count++;
                    }
                    catch { }
                }
                if (count >= limit) break;
            }
            return list;
        }

        private void EnsureDefaultRules()
        {
            if (File.Exists(rulesPath)) return;
            var lines = new[]
            {
                "Name\tPathContains\tCategory\tRisk\tAction\tRecommendation\tReason",
                "Chrome 浏览器缓存\t\\Google\\Chrome\\User Data\\\tBrowserCache\tLow\tClean\t可清理\t浏览器缓存，可重新生成。",
                "Edge 浏览器缓存\t\\Microsoft\\Edge\\User Data\\\tBrowserCache\tLow\tClean\t可清理\t浏览器缓存，可重新生成。",
                "用户临时目录\t\\AppData\\Local\\Temp\\\tTemp\tLow\tClean\t可清理\t用户临时文件，通常可清理。",
                "崩溃转储\t\\CrashDumps\\\tDump\tLow\tClean\t可清理\t软件崩溃转储，可按需清理。",
                "WinSxS 组件存储\t\\Windows\\WinSxS\\\tWindowsComponent\tBlocked\tLocked\t官方清理\tWindows 组件存储，禁止手动删除。",
                "Windows Installer\t\\Windows\\Installer\\\tWindowsInstaller\tBlocked\tLocked\t禁止\t安装缓存影响修复、卸载和更新，禁止手动删除。",
                "Windows 更新缓存\t\\Windows\\SoftwareDistribution\\Download\\\tWindowsUpdate\tMedium\tReview\t官方清理\tWindows 更新下载缓存，建议使用系统官方清理。",
                "Docker 数据\t\\AppData\\Local\\Docker\\\tDeveloperData\tMedium\tReview\t专项清理\tDocker 镜像和构建缓存应使用 Docker prune 或迁移数据目录。",
                "npm 缓存\t\\AppData\\Local\\npm-cache\\\tDeveloperCache\tLow\tReview\t专项清理\tnpm 缓存建议使用 npm cache clean。",
                "pip 缓存\t\\AppData\\Local\\pip\\Cache\\\tDeveloperCache\tLow\tReview\t专项清理\tpip 缓存建议使用 pip cache purge。",
                "Gradle 缓存\t\\.gradle\\caches\\\tDeveloperCache\tMedium\tReview\t专项清理\tGradle 缓存可清理但会重新下载依赖。",
                "Maven 仓库\t\\.m2\\repository\\\tDeveloperCache\tMedium\tReview\t迁移\tMaven 本地仓库可迁移，不建议直接删除。",
                "微信文件\t\\Documents\\WeChat Files\\\tUserData\tHigh\tKeep\t迁移\t聊天文件通常很大，但属于用户数据，建议迁移。",
                "QQ 文件\t\\Documents\\Tencent Files\\\tUserData\tHigh\tKeep\t迁移\t聊天文件属于用户数据，建议迁移。"
            };
            File.WriteAllLines(rulesPath, lines, Encoding.UTF8);
        }

        private void EnsureWhitelist()
        {
            if (File.Exists(whitelistPath)) return;
            File.WriteAllLines(whitelistPath, new[] { "Path\tReason\tCreatedAt" }, Encoding.UTF8);
        }

        private List<string> LoadWhitelist()
        {
            EnsureWhitelist();
            return File.ReadAllLines(whitelistPath, Encoding.UTF8)
                .Skip(1)
                .Select(line => line.Split('\t').FirstOrDefault() ?? "")
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(ExpandRulePath)
                .ToList();
        }

        private bool IsWhitelisted(string path)
        {
            string full = NormalizePath(path).ToLowerInvariant();
            foreach (string entry in LoadWhitelist())
            {
                string rule = NormalizePath(entry).ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(rule) && full.StartsWith(rule)) return true;
            }
            return false;
        }

        private List<CleanerRule> LoadCleanerRules()
        {
            var rules = new List<CleanerRule>();
            if (!File.Exists(rulesPath)) return rules;
            foreach (string line in File.ReadAllLines(rulesPath, Encoding.UTF8).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] p = line.Split('\t');
                if (p.Length < 7) continue;
                rules.Add(new CleanerRule
                {
                    Name = p[0],
                    PathContains = ExpandRulePath(p[1]),
                    Category = p[2],
                    Risk = p[3],
                    Action = p[4],
                    Recommendation = p[5],
                    Reason = p[6]
                });
            }
            return rules;
        }

        private string ExpandRulePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return value
                .Replace("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData))
                .Replace("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
                .Replace("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                .Replace("%SYSTEMDRIVE%", Environment.GetEnvironmentVariable("SystemDrive") ?? "C:");
        }

        private CleanerRule MatchRule(string path)
        {
            string lower = path.ToLowerInvariant();
            foreach (var rule in cleanerRules)
            {
                if (string.IsNullOrWhiteSpace(rule.PathContains)) continue;
                if (lower.Contains(rule.PathContains.ToLowerInvariant())) return rule;
            }
            return null;
        }

        private List<InstalledSoftware> LoadInstalledSoftware()
        {
            var result = new List<InstalledSoftware>();
            string[] roots =
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (string root in roots)
            {
                LoadSoftwareFromRegistry(Registry.LocalMachine, root, result);
                LoadSoftwareFromRegistry(Registry.CurrentUser, root, result);
            }

            return result
                .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                .Where(s => !string.IsNullOrWhiteSpace(s.InstallLocation) || !string.IsNullOrWhiteSpace(s.UninstallString))
                .GroupBy(s => (s.Name + "|" + s.InstallLocation).ToLowerInvariant())
                .Select(g => g.First())
                .ToList();
        }

        private void LoadSoftwareFromRegistry(RegistryKey hive, string path, List<InstalledSoftware> result)
        {
            try
            {
                using (RegistryKey key = hive.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (string subName in key.GetSubKeyNames())
                    {
                        using (RegistryKey sub = key.OpenSubKey(subName))
                        {
                            if (sub == null) continue;
                            string name = Convert.ToString(sub.GetValue("DisplayName"));
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            result.Add(new InstalledSoftware
                            {
                                Name = name,
                                Publisher = Convert.ToString(sub.GetValue("Publisher")),
                                InstallLocation = NormalizePath(Convert.ToString(sub.GetValue("InstallLocation"))),
                                UninstallString = Convert.ToString(sub.GetValue("UninstallString"))
                            });
                        }
                    }
                }
            }
            catch { }
        }

        private List<ServiceReference> LoadServiceReferences()
        {
            var services = new List<ServiceReference>();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, DisplayName, PathName FROM Win32_Service"))
                {
                    foreach (ManagementObject service in searcher.Get())
                    {
                        services.Add(new ServiceReference
                        {
                            Name = Convert.ToString(service["Name"]),
                            DisplayName = Convert.ToString(service["DisplayName"]),
                            PathName = Convert.ToString(service["PathName"])
                        });
                    }
                }
            }
            catch { }
            return services;
        }

        private IEnumerable<string> EnumerateFilesSafe(string root)
        {
            var pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                string current = pending.Pop();
                string[] files = new string[0];
                string[] directories = new string[0];

                try
                {
                    files = Directory.GetFiles(current);
                }
                catch
                {
                    skippedDirectories++;
                }

                foreach (string file in files)
                {
                    yield return file;
                }

                try
                {
                    directories = Directory.GetDirectories(current);
                }
                catch
                {
                    skippedDirectories++;
                }

                foreach (string directory in directories)
                {
                    if (!IsProtectedPath(directory))
                    {
                        pending.Push(directory);
                    }
                }
            }
        }

        private List<ScanTarget> GetScanTargets(string mode)
        {
            string drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string temp = Path.GetTempPath();
            var targets = new List<ScanTarget>();

            AddTarget(targets, temp, "User Temp", "SoftwareGenerated", "Low", "Clean", "Current user temporary files.");
            AddTarget(targets, Path.Combine(drive + "\\", "Windows", "Temp"), "Windows Temp", "SystemTemp", "Low", "Clean", "Windows temporary files, excluding protected locked files.");
            AddTarget(targets, Path.Combine(local, "Temp"), "Local Temp", "SoftwareGenerated", "Low", "Clean", "Per-user application temporary files.");
            AddTarget(targets, Path.Combine(local, "CrashDumps"), "Crash Dumps", "SoftwareGenerated", "Low", "Clean", "Application crash dumps.");
            AddTarget(targets, Path.Combine(local, "Google", "Chrome", "User Data", "Default", "Cache"), "Chrome", "SoftwareGenerated", "Low", "Clean", "Browser cache can be regenerated.");
            AddTarget(targets, Path.Combine(local, "Microsoft", "Edge", "User Data", "Default", "Cache"), "Edge", "SoftwareGenerated", "Low", "Clean", "Browser cache can be regenerated.");
            AddTarget(targets, Path.Combine(roaming, "Mozilla", "Firefox", "Profiles"), "Firefox", "SoftwareGenerated", "Medium", "Review", "Firefox profile tree may contain cache and user data.");

            if (mode == "SoftwareLeftover" || mode == "Deep")
            {
                AddTarget(targets, local, "Local AppData", "SoftwareGenerated", "Medium", "Review", "Application-generated data. Review before cleanup.");
                AddTarget(targets, roaming, "Roaming AppData", "SoftwareGenerated", "High", "Keep", "May contain app settings, profiles, saves, and databases.");
                AddTarget(targets, Path.Combine(drive + "\\", "ProgramData"), "ProgramData", "SoftwareGenerated", "Medium", "Review", "Shared application data. Review before cleanup.");
            }

            if (mode == "Deep")
            {
                AddTarget(targets, Path.Combine(drive + "\\", "Users"), "Users", "UserFile", "High", "Keep", "User files are not automatically cleaned.");
                AddTarget(targets, Path.Combine(drive + "\\", "Program Files"), "Program Files", "SoftwareGenerated", "High", "Keep", "Installed application files are not cleanup candidates.");
                AddTarget(targets, Path.Combine(drive + "\\", "Program Files (x86)"), "Program Files", "SoftwareGenerated", "High", "Keep", "Installed application files are not cleanup candidates.");
            }

            return targets.Where(t => Directory.Exists(t.Path)).GroupBy(t => t.Path.ToLowerInvariant()).Select(g => g.First()).ToList();
        }

        private void AddTarget(List<ScanTarget> targets, string path, string source, string category, string risk, string action, string reason)
        {
            if (!string.IsNullOrWhiteSpace(path))
                targets.Add(new ScanTarget { Path = path, Source = source, Category = category, Risk = risk, Action = action, Reason = reason });
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            path = path.Trim().Trim('"');
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }

        private InstalledSoftware FindOwnerSoftware(string path)
        {
            string full = NormalizePath(path);
            InstalledSoftware best = null;
            int bestLength = 0;
            foreach (var app in installedSoftware)
            {
                string root = NormalizePath(app.InstallLocation);
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && root.Length > bestLength)
                {
                    best = app;
                    bestLength = root.Length;
                }
            }
            return best;
        }

        private ServiceReference FindServiceReference(string path)
        {
            string lower = path.ToLowerInvariant();
            foreach (var service in serviceReferences)
            {
                if (string.IsNullOrWhiteSpace(service.PathName)) continue;
                string servicePath = ExtractExecutablePath(service.PathName).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(servicePath)) continue;
                string serviceDir = "";
                try { serviceDir = Path.GetDirectoryName(servicePath); } catch { }
                if (lower == servicePath || (!string.IsNullOrWhiteSpace(serviceDir) && lower.StartsWith(serviceDir.ToLowerInvariant() + "\\")))
                {
                    return service;
                }
            }
            return null;
        }

        private string ExtractExecutablePath(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return "";
            commandLine = commandLine.Trim();
            if (commandLine.StartsWith("\""))
            {
                int end = commandLine.IndexOf('"', 1);
                if (end > 1) return NormalizePath(commandLine.Substring(1, end - 1));
            }

            int exe = commandLine.ToLowerInvariant().IndexOf(".exe");
            if (exe >= 0) return NormalizePath(commandLine.Substring(0, exe + 4));
            return NormalizePath(commandLine.Split(' ')[0]);
        }

        private bool IsSignatureCandidate(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string[] exts = { ".exe", ".dll", ".sys", ".msi", ".ocx", ".scr", ".cat" };
            return exts.Contains(ext);
        }

        private void ReadSignatureInfo(string path, out string status, out string publisher)
        {
            status = "NotChecked";
            publisher = "";
            if (!IsSignatureCandidate(path)) return;

            try
            {
                X509Certificate cert = X509Certificate.CreateFromSignedFile(path);
                X509Certificate2 cert2 = new X509Certificate2(cert);
                publisher = cert2.GetNameInfo(X509NameType.SimpleName, false);
                status = string.IsNullOrWhiteSpace(publisher) ? "Signed" : "Signed";
            }
            catch
            {
                status = "UnsignedOrInvalid";
            }
        }

        private bool IsMicrosoftPublisher(string publisher)
        {
            return !string.IsNullOrWhiteSpace(publisher) &&
                   publisher.IndexOf("Microsoft", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }

        private FileFinding GetFindingForFile(FileInfo file, ScanTarget target)
        {
            string category = target.Category;
            string risk = target.Risk;
            string action = target.Action;
            string reason = target.Reason;
            string path = file.FullName;
            int score = 50;
            var notes = new List<string>();
            string signatureStatus;
            string publisher;
            ReadSignatureInfo(path, out signatureStatus, out publisher);
            InstalledSoftware owner = FindOwnerSoftware(path);
            ServiceReference service = FindServiceReference(path);
            CleanerRule rule = MatchRule(path);

            if (rule != null)
            {
                category = rule.Category;
                risk = rule.Risk;
                action = rule.Action;
                reason = rule.Reason;
                notes.Add("命中规则库：" + rule.Name + "，建议：" + rule.Recommendation);
                if (rule.Risk == "Blocked") score += 100;
                else if (rule.Risk == "High") score += 70;
                else if (rule.Risk == "Medium") score += 25;
                else if (rule.Risk == "Low") score -= 30;
            }

            if (IsProtectedPath(path))
            {
                category = "SystemProtected";
                risk = "Blocked";
                action = "Locked";
                reason = "Protected Windows path.";
                score += 100;
                notes.Add("命中系统保护路径");
            }
            else if (IsTempExtension(file.Extension))
            {
                if (risk != "High")
                {
                    risk = "Low";
                    action = "Clean";
                    reason = "Common regenerable temporary, log, dump, or backup extension.";
                    score -= 20;
                    notes.Add("临时/日志/转储类扩展名");
                }
            }
            else if (IsCachePath(path))
            {
                if (risk != "High")
                {
                    risk = "Low";
                    action = "Clean";
                    reason = "Cache or temp path can usually be regenerated.";
                    score -= 40;
                    notes.Add("缓存或临时目录");
                }
            }
            else if (IsUserContentPath(path))
            {
                category = "UserFile";
                risk = "High";
                action = "Keep";
                reason = "User-owned content is not automatically cleaned.";
                score += 80;
                notes.Add("用户个人文件目录");
            }

            if (owner != null)
            {
                score += 40;
                notes.Add("命中已安装软件：" + owner.Name);
                if (!IsCachePath(path) && !IsTempExtension(file.Extension))
                {
                    risk = "High";
                    action = "Keep";
                    reason = "Installed application ownership matched. Keep by default.";
                }
            }

            if (service != null)
            {
                score += 90;
                risk = "Blocked";
                action = "Locked";
                reason = "Referenced by Windows service.";
                notes.Add("被服务引用：" + service.DisplayName);
            }

            if (IsMicrosoftPublisher(publisher))
            {
                score += 80;
                risk = "Blocked";
                action = "Locked";
                reason = "Microsoft signed file.";
                notes.Add("Microsoft 数字签名");
            }
            else if (signatureStatus == "Signed")
            {
                score += 25;
                notes.Add("有效数字签名：" + publisher);
                if (!IsCachePath(path) && !IsTempExtension(file.Extension))
                {
                    risk = "Medium";
                    action = "Review";
                }
            }
            else if (signatureStatus == "UnsignedOrInvalid" && IsProtectedPath(path))
            {
                score += 70;
                risk = "Blocked";
                action = "Locked";
                notes.Add("系统路径中的未签名或签名无效文件");
            }

            bool locked = IsLocked(path);
            if (locked && action == "Clean")
            {
                action = "Skip";
                reason += " File appears to be in use.";
                score += 30;
                notes.Add("文件正在被占用");
            }

            if (score >= 90 && action == "Clean")
            {
                risk = "High";
                action = "Keep";
                reason = "Risk score is high. Keep by default.";
            }
            else if (score >= 60 && action == "Clean")
            {
                risk = "Medium";
                action = "Review";
                reason = "Risk score requires manual review.";
            }

            return new FileFinding
            {
                Path = path,
                Size = file.Length,
                Category = category,
                Source = target.Source,
                Risk = risk,
                RecommendedAction = action,
                Reason = reason,
                LastWriteTime = file.LastWriteTime,
                Locked = locked,
                Recoverable = action == "Clean" || action == "Review",
                SignatureStatus = signatureStatus,
                Publisher = publisher,
                OwnerSoftware = owner == null ? "" : owner.Name,
                ServiceReference = service == null ? "" : service.DisplayName,
                SafetyNotes = string.Join("; ", notes.ToArray()),
                RiskScore = score
            };
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
                Path.Combine(drive + "\\", "Program Files", "WindowsApps")
            };
            return roots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsTempExtension(string ext)
        {
            string[] exts = { ".tmp", ".temp", ".log", ".dmp", ".etl", ".bak", ".old" };
            return exts.Contains((ext ?? "").ToLowerInvariant());
        }

        private bool IsCachePath(string path)
        {
            string p = path.ToLowerInvariant();
            return p.Contains("\\cache\\") || p.Contains("\\code cache\\") || p.Contains("\\gpucache\\") || p.Contains("\\temp\\");
        }

        private bool IsUserContentPath(string path)
        {
            string p = path.ToLowerInvariant();
            return p.Contains("\\documents\\") || p.Contains("\\desktop\\") || p.Contains("\\pictures\\") ||
                   p.Contains("\\videos\\") || p.Contains("\\music\\") || p.Contains("\\downloads\\");
        }

        private bool IsLocked(string path)
        {
            try
            {
                using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                return false;
            }
            catch { return true; }
        }

        private void BindGrid()
        {
            grid.DataSource = null;
            grid.DataSource = findings.Select(f => new
            {
                编号 = f.Id,
                来源 = ToZhSource(f.Source),
                分类 = ToZhCategory(f.Category),
                风险 = ToZhRisk(f.Risk),
                建议操作 = ToZhAction(f.RecommendedAction),
                风险分 = f.RiskScore,
                软件归属 = string.IsNullOrWhiteSpace(f.OwnerSoftware) ? "-" : f.OwnerSoftware,
                签名 = ToZhSignature(f.SignatureStatus, f.Publisher),
                服务引用 = string.IsNullOrWhiteSpace(f.ServiceReference) ? "-" : f.ServiceReference,
                大小 = f.SizeText,
                修改时间 = f.LastWriteTime,
                路径 = f.Path
            }).ToList();

            if (grid.Columns["路径"] != null) grid.Columns["路径"].FillWeight = 280;
            if (grid.Columns["编号"] != null) grid.Columns["编号"].FillWeight = 55;
            if (grid.Columns["大小"] != null) grid.Columns["大小"].FillWeight = 70;
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (grid.Columns[e.ColumnIndex].Name != "风险" || e.Value == null) return;
            string risk = e.Value.ToString();
            if (risk == "低风险") e.CellStyle.BackColor = Color.FromArgb(226, 246, 232);
            else if (risk == "中风险") e.CellStyle.BackColor = Color.FromArgb(255, 244, 214);
            else if (risk == "高风险") e.CellStyle.BackColor = Color.FromArgb(255, 229, 229);
            else if (risk == "已保护") e.CellStyle.BackColor = Color.FromArgb(230, 230, 230);
        }

        private void UpdateSummary()
        {
            long safe = findings.Where(f => f.RecommendedAction == "Clean").Sum(f => f.Size);
            long review = findings.Where(f => f.RecommendedAction == "Review").Sum(f => f.Size);
            long keep = findings.Where(f => f.RecommendedAction == "Keep").Sum(f => f.Size);
            long blocked = findings.Where(f => f.RecommendedAction == "Locked").Sum(f => f.Size);
            summaryLabel.Text = string.Format("可安全清理：{0}    需要确认：{1}    建议保留：{2}    系统保护：{3}",
                FileFinding.FormatBytes(safe), FileFinding.FormatBytes(review), FileFinding.FormatBytes(keep), FileFinding.FormatBytes(blocked));
        }

        private void UpdateDetails()
        {
            if (grid.SelectedRows.Count == 0) return;
            string id = grid.SelectedRows[0].Cells["编号"].Value.ToString();
            var f = findings.FirstOrDefault(x => x.Id == id);
            if (f == null) return;
            detailBox.Text =
                "路径：" + f.Path + Environment.NewLine +
                "来源：" + ToZhSource(f.Source) + Environment.NewLine +
                "分类：" + ToZhCategory(f.Category) + Environment.NewLine +
                "风险：" + ToZhRisk(f.Risk) + "    建议操作：" + ToZhAction(f.RecommendedAction) + Environment.NewLine +
                "风险分：" + f.RiskScore + "    数字签名：" + ToZhSignature(f.SignatureStatus, f.Publisher) + Environment.NewLine +
                "软件归属：" + (string.IsNullOrWhiteSpace(f.OwnerSoftware) ? "-" : f.OwnerSoftware) + Environment.NewLine +
                "服务引用：" + (string.IsNullOrWhiteSpace(f.ServiceReference) ? "-" : f.ServiceReference) + Environment.NewLine +
                "大小：" + f.SizeText + "    修改时间：" + f.LastWriteTime + Environment.NewLine +
                "原因：" + ToZhReason(f.Reason) + Environment.NewLine +
                "安全说明：" + (string.IsNullOrWhiteSpace(f.SafetyNotes) ? "-" : f.SafetyNotes);
        }

        private void ExportCsv()
        {
            if (findings.Count == 0) return;
            string path = Path.Combine(reportRoot, "scan-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
            SaveCsv(path, findings);
            MessageBox.Show("报告已导出：\n" + path, "导出报告", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveCsv(string path, IEnumerable<FileFinding> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Id,Path,Size,SizeText,Category,Source,Risk,RecommendedAction,RiskScore,SignatureStatus,Publisher,OwnerSoftware,ServiceReference,SafetyNotes,Reason,LastWriteTime,Locked,Recoverable");
            foreach (var f in rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(f.Id), Csv(f.Path), f.Size.ToString(), Csv(f.SizeText), Csv(f.Category), Csv(f.Source),
                    Csv(f.Risk), Csv(f.RecommendedAction), f.RiskScore.ToString(), Csv(f.SignatureStatus), Csv(f.Publisher),
                    Csv(f.OwnerSoftware), Csv(f.ServiceReference), Csv(f.SafetyNotes), Csv(f.Reason), Csv(f.LastWriteTime.ToString("s")),
                    Csv(f.Locked.ToString()), Csv(f.Recoverable.ToString())
                }));
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        public string RunSelfTest()
        {
            var log = new StringBuilder();
            log.AppendLine("SELF_TEST_START");
            log.AppendLine("DataRoot=" + dataRoot);
            log.AppendLine("Rules=" + cleanerRules.Count);

            string testRoot = Path.Combine(dataRoot, "self-test");
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            Directory.CreateDirectory(testRoot);
            string tempDir = Path.Combine(testRoot, "AppData", "Local", "Temp");
            Directory.CreateDirectory(tempDir);
            string file1 = Path.Combine(tempDir, "sample.tmp");
            string file2 = Path.Combine(tempDir, "sample.log");
            File.WriteAllText(file1, "temporary test data", Encoding.UTF8);
            File.WriteAllText(file2, "log test data", Encoding.UTF8);

            cleanerRules.Add(new CleanerRule
            {
                Name = "SelfTest Temp",
                PathContains = "\\self-test\\AppData\\Local\\Temp\\",
                Category = "Temp",
                Risk = "Low",
                Action = "Clean",
                Recommendation = "可清理",
                Reason = "自检临时文件。"
            });

            var target = new ScanTarget
            {
                Path = tempDir,
                Source = "SelfTest",
                Category = "SoftwareGenerated",
                Risk = "Low",
                Action = "Clean",
                Reason = "Self test temp files."
            };

            findings.Clear();
            int id = 1;
            foreach (string path in EnumerateFilesSafe(tempDir))
            {
                var f = GetFindingForFile(new FileInfo(path), target);
                f.Id = id.ToString("D6");
                findings.Add(f);
                id++;
            }
            log.AppendLine("Findings=" + findings.Count);
            log.AppendLine("CleanCandidates=" + findings.Count(f => f.Risk == "Low" && f.RecommendedAction == "Clean"));

            string report = Path.Combine(reportRoot, "self-test-scan.csv");
            SaveCsv(report, findings);
            log.AppendLine("ReportExists=" + File.Exists(report));

            var diagnostics = RunSpaceDiagnosis();
            log.AppendLine("Diagnostics=" + diagnostics.Count);
            string diagReport = Path.Combine(reportRoot, "self-test-diagnosis.csv");
            SaveSpaceDiagnosisCsv(diagReport, diagnostics.Take(5));
            log.AppendLine("DiagnosisReportExists=" + File.Exists(diagReport));

            var manifest = LoadManifest();
            var first = findings.First(f => f.Risk == "Low" && f.RecommendedAction == "Clean");
            string sha = ComputeSha256(first.Path);
            string dest = Path.Combine(quarantineRoot, NewQuarantineName(first.Path));
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(first.Path, dest);
            string qid = Guid.NewGuid().ToString();
            manifest.Add(new QuarantineItem
            {
                Id = qid,
                OriginalPath = first.Path,
                QuarantinePath = dest,
                Size = first.Size,
                Source = first.Source,
                Risk = first.Risk,
                CleanedAt = DateTime.Now,
                ExpiresAt = DateTime.Now.AddDays(7),
                Sha256 = sha,
                SnapshotReason = first.Reason
            });
            SaveManifest(manifest);
            log.AppendLine("Quarantined=" + File.Exists(dest));

            RestoreItemWithoutUi(qid);
            log.AppendLine("Restored=" + File.Exists(first.Path));

            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
            log.AppendLine("SELF_TEST_OK");
            return log.ToString();
        }

        public string ExportModernUiJson(string outputPath, string mode, int limit)
        {
            installedSoftware = LoadInstalledSoftware();
            serviceReferences = LoadServiceReferences();
            findings.Clear();
            findings.AddRange(Scan(mode, limit));
            var diagnostics = RunSpaceDiagnosis();

            long safe = findings.Where(f => f.RecommendedAction == "Clean" && f.Risk == "Low").Sum(f => f.Size);
            long review = findings.Where(f => f.RecommendedAction == "Review" || f.Risk == "Medium").Sum(f => f.Size);
            long keep = findings.Where(f => f.RecommendedAction == "Keep" || f.Risk == "High").Sum(f => f.Size);
            long blocked = findings.Where(f => f.RecommendedAction == "Locked" || f.Risk == "Blocked").Sum(f => f.Size);
            long migrate = diagnostics.Where(d => d.ActionType == "迁移").Sum(d => d.Size);
            long official = diagnostics.Where(d => d.ActionType == "官方清理").Sum(d => d.Size);
            long special = diagnostics.Where(d => d.ActionType == "专项清理").Sum(d => d.Size);
            long forbid = diagnostics.Where(d => d.ActionType == "禁止").Sum(d => d.Size);

            string driveName = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            DriveInfo drive = new DriveInfo(driveName + "\\");
            long used = drive.TotalSize - drive.AvailableFreeSpace;
            int usedPercent = drive.TotalSize > 0 ? (int)Math.Round((used * 100.0) / drive.TotalSize) : 0;

            var sb = new StringBuilder();
            sb.AppendLine("{");
            bool admin = IsAdministrator();
            sb.AppendLine("  \"meta\": { \"mode\": \"" + Json(ToZhScanMode(mode)) + "\", \"source\": \"真实扫描\", \"rules\": " + cleanerRules.Count + ", \"lastScan\": \"" + Json(DateTime.Now.ToString("HH:mm")) + "\", \"admin\": " + (admin ? "true" : "false") + ", \"adminStatus\": \"" + Json(admin ? "管理员模式" : "普通模式，部分系统目录可能无法扫描") + "\", \"skippedDirectories\": " + skippedDirectories + " },");
            sb.AppendLine("  \"drive\": { \"name\": \"" + Json(driveName) + "\", \"total\": \"" + Json(FileFinding.FormatBytes(drive.TotalSize)) + "\", \"used\": \"" + Json(FileFinding.FormatBytes(used)) + "\", \"free\": \"" + Json(FileFinding.FormatBytes(drive.AvailableFreeSpace)) + "\", \"usedPercent\": " + usedPercent + " },");
            sb.AppendLine("  \"metrics\": [");
            sb.AppendLine("    { \"label\": \"系统盘已用\", \"value\": \"" + Json(FileFinding.FormatBytes(used)) + "\", \"hint\": \"总容量 " + Json(FileFinding.FormatBytes(drive.TotalSize)) + "\" },");
            sb.AppendLine("    { \"label\": \"预计可安全清理\", \"value\": \"" + Json(FileFinding.FormatBytes(safe)) + "\", \"hint\": \"低风险缓存与临时文件\" },");
            sb.AppendLine("    { \"label\": \"建议迁移空间\", \"value\": \"" + Json(FileFinding.FormatBytes(migrate)) + "\", \"hint\": \"用户文件、聊天文件、项目数据\" },");
            sb.AppendLine("    { \"label\": \"需官方处理\", \"value\": \"" + Json(FileFinding.FormatBytes(official)) + "\", \"hint\": \"Windows 更新与组件存储\" }");
            sb.AppendLine("  ],");
            sb.AppendLine("  \"usageBars\": [");
            AppendUsageBar(sb, "用户数据", migrate, used, "green", true);
            AppendUsageBar(sb, "软件与缓存", safe + review + special, used, "blue", true);
            AppendUsageBar(sb, "Windows 组件", official, used, "amber", true);
            AppendUsageBar(sb, "禁止手动处理", forbid + blocked, used, "red", true);
            sb.AppendLine("  ],");
            sb.AppendLine("  \"recommendations\": [");
            var recs = diagnostics.OrderByDescending(d => d.Size).Take(3).ToList();
            for (int i = 0; i < recs.Count; i++)
            {
                var d = recs[i];
                sb.Append("    { \"title\": \"" + Json(d.Area) + "\", \"size\": \"" + Json(FileFinding.FormatBytes(d.Size)) + "\", \"action\": \"" + Json(d.ActionType) + "\", \"detail\": \"" + Json(d.Reason) + "\" }");
                sb.AppendLine(i == recs.Count - 1 ? "" : ",");
            }
            sb.AppendLine("  ],");
            AppendDiagnosticsJson(sb, diagnostics);
            sb.AppendLine(",");
            AppendCleanupJson(sb, findings);
            sb.AppendLine(",");
            AppendMigrationJson(sb, diagnostics);
            sb.AppendLine(",");
            AppendOfficialJson(sb, diagnostics);
            sb.AppendLine(",");
            AppendQuarantineJson(sb);
            sb.AppendLine(",");
            AppendReportsJson(sb);
            sb.AppendLine(",");
            AppendRulesJson(sb);
            sb.AppendLine("}");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            string jsonText = sb.ToString().Replace(",\r\n  ]", "\r\n  ]").Replace(",\n  ]", "\n  ]").Replace(",\r\n}", "\r\n}").Replace(",\n}", "\n}");
            File.WriteAllText(outputPath, jsonText, new UTF8Encoding(false));
            return outputPath;
        }

        private void AppendUsageBar(StringBuilder sb, string name, long size, long total, string tone, bool comma)
        {
            int percent = total > 0 ? Math.Min(100, Math.Max(0, (int)Math.Round(size * 100.0 / total))) : 0;
            sb.Append("    { \"name\": \"" + Json(name) + "\", \"size\": \"" + Json(FileFinding.FormatBytes(size)) + "\", \"percent\": " + percent + ", \"tone\": \"" + Json(tone) + "\" }");
            sb.AppendLine(comma ? "," : "");
        }

        private void AppendDiagnosticsJson(StringBuilder sb, List<SpaceDiagnosticItem> diagnostics)
        {
            sb.AppendLine("  \"diagnosis\": [");
            var rows = diagnostics.OrderByDescending(d => d.Size).Take(30).ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var d = rows[i];
                sb.Append("    { \"area\": \"" + Json(d.Area) + "\", \"size\": \"" + Json(FileFinding.FormatBytes(d.Size)) + "\", \"action\": \"" + Json(d.ActionType) + "\", \"risk\": \"" + Json(d.Risk) + "\", \"path\": \"" + Json(d.Path) + "\", \"recommendation\": \"" + Json(d.Recommendation) + "\", \"reason\": \"" + Json(d.Reason) + "\", \"riskExplanation\": \"" + Json(d.RiskExplanation) + "\", \"specialKey\": \"" + Json(d.SpecialKey) + "\", \"specialCommand\": \"" + Json(d.SpecialCommand) + "\" }");
                sb.AppendLine(i == rows.Count - 1 ? "" : ",");
            }
            sb.Append("  ]");
        }

        private void AppendCleanupJson(StringBuilder sb, List<FileFinding> rowsSource)
        {
            sb.AppendLine("  \"cleanup\": [");
            var rows = rowsSource.Where(f => f.RecommendedAction == "Clean" && f.Risk == "Low" && !IsWhitelisted(f.Path)).OrderByDescending(f => f.Size).Take(30).ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var f = rows[i];
                sb.Append("    { \"id\": \"" + Json(f.Id) + "\", \"name\": \"" + Json(Path.GetFileName(f.Path)) + "\", \"path\": \"" + Json(f.Path) + "\", \"source\": \"" + Json(ToZhSource(f.Source)) + "\", \"owner\": \"" + Json(GuessSoftwareOwner(f)) + "\", \"publisher\": \"" + Json(string.IsNullOrWhiteSpace(f.Publisher) ? "-" : f.Publisher) + "\", \"signature\": \"" + Json(ToZhSignature(f.SignatureStatus, f.Publisher)) + "\", \"risk\": \"低风险\", \"size\": \"" + Json(f.SizeText) + "\", \"reason\": \"" + Json(ToZhReason(f.Reason)) + "\", \"lastWrite\": \"" + Json(f.LastWriteTime.ToString("yyyy-MM-dd HH:mm")) + "\", \"snapshot\": \"" + Json(f.SafetyNotes) + "\", \"recoverable\": true }");
                sb.AppendLine(i == rows.Count - 1 ? "" : ",");
            }
            sb.Append("  ]");
        }

        private void AppendMigrationJson(StringBuilder sb, List<SpaceDiagnosticItem> diagnostics)
        {
            sb.AppendLine("  \"migration\": [");
            var rows = diagnostics.Where(d => d.ActionType == "迁移").OrderByDescending(d => d.Size).Take(12).ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var d = rows[i];
                string target = SuggestMigrationTarget(d);
                string steps = "1. 确认目标盘剩余空间大于 " + FileFinding.FormatBytes(d.Size) + "；2. 将原路径内容复制到建议目标；3. 对比文件数量和目录大小；4. 在对应软件或系统设置里修改保存路径；5. 保留原目录 7 天，确认无异常后再手动处理。";
                string risk = "这是迁移建议，不是删除建议。不要直接删除原路径，尤其是 AppData、聊天文件、项目文件和软件数据。";
                sb.Append("    { \"title\": \"" + Json(d.Area) + "\", \"size\": \"" + Json(FileFinding.FormatBytes(d.Size)) + "\", \"path\": \"" + Json(d.Path) + "\", \"target\": \"" + Json(target) + "\", \"detail\": \"" + Json(d.Recommendation + "。" + d.Reason) + "\", \"steps\": \"" + Json(steps) + "\", \"risk\": \"" + Json(risk) + "\" }");
                sb.AppendLine(i == rows.Count - 1 ? "" : ",");
            }
            sb.Append("  ]");
        }

        private string SuggestMigrationTarget(SpaceDiagnosticItem item)
        {
            string name = SanitizeFileName(item.Area);
            if (item.Path.IndexOf("\\Downloads", StringComparison.OrdinalIgnoreCase) >= 0) return "D:\\Downloads";
            if (item.Path.IndexOf("\\Desktop", StringComparison.OrdinalIgnoreCase) >= 0) return "D:\\Desktop";
            if (item.Path.IndexOf("\\Documents", StringComparison.OrdinalIgnoreCase) >= 0) return "D:\\Documents\\" + name;
            if (item.Path.IndexOf("\\Videos", StringComparison.OrdinalIgnoreCase) >= 0) return "D:\\Videos\\" + name;
            if (item.Path.IndexOf("WeChat Files", StringComparison.OrdinalIgnoreCase) >= 0) return "D:\\MYL_Migrated\\WeChat Files";
            if (item.Path.IndexOf("Tencent Files", StringComparison.OrdinalIgnoreCase) >= 0) return "D:\\MYL_Migrated\\Tencent Files";
            if (item.Path.IndexOf("\\.m2\\", StringComparison.OrdinalIgnoreCase) >= 0) return "D:\\DevCache\\.m2\\repository";
            if (item.Path.IndexOf("\\Packages", StringComparison.OrdinalIgnoreCase) >= 0) return "D:\\MYL_Migrated\\WSL-Packages";
            return "D:\\MYL_Migrated\\" + name;
        }

        private string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Data";
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace(" ", "_");
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

        private string ToZhScanMode(string mode)
        {
            if (string.Equals(mode, "Deep", StringComparison.OrdinalIgnoreCase)) return "深度诊断";
            if (string.Equals(mode, "SoftwareLeftover", StringComparison.OrdinalIgnoreCase)) return "软件残留扫描";
            return "快速扫描";
        }

        private string GuessSoftwareOwner(FileFinding finding)
        {
            string path = finding.Path ?? "";
            if (!string.IsNullOrWhiteSpace(finding.OwnerSoftware)) return finding.OwnerSoftware;
            if (!string.IsNullOrWhiteSpace(finding.Publisher) && finding.Publisher != "-") return finding.Publisher;
            if (path.IndexOf("WeChat", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("微信", StringComparison.OrdinalIgnoreCase) >= 0) return "微信";
            if (path.IndexOf("Tencent Files", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\Tencent\\", StringComparison.OrdinalIgnoreCase) >= 0) return "腾讯/QQ";
            if (path.IndexOf("\\Google\\Chrome\\", StringComparison.OrdinalIgnoreCase) >= 0) return "Google Chrome";
            if (path.IndexOf("\\Microsoft\\Edge\\", StringComparison.OrdinalIgnoreCase) >= 0) return "Microsoft Edge";
            if (path.IndexOf("JianyingPro", StringComparison.OrdinalIgnoreCase) >= 0) return "剪映专业版";
            if (path.IndexOf("DingTalk", StringComparison.OrdinalIgnoreCase) >= 0) return "钉钉";
            if (path.IndexOf("\\OpenAI\\", StringComparison.OrdinalIgnoreCase) >= 0) return "OpenAI / ChatGPT";
            if (path.IndexOf("\\pip\\", StringComparison.OrdinalIgnoreCase) >= 0) return "Python pip";
            if (path.IndexOf("python", StringComparison.OrdinalIgnoreCase) >= 0) return "Python";
            if (path.IndexOf("npm-cache", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("\\nodejs\\", StringComparison.OrdinalIgnoreCase) >= 0) return "Node.js / npm";
            if (path.IndexOf("ShadowBot", StringComparison.OrdinalIgnoreCase) >= 0) return "影刀 RPA";
            if (path.IndexOf("EBWebView", StringComparison.OrdinalIgnoreCase) >= 0) return "WebView2 应用缓存";
            if (path.IndexOf("\\Temp\\", StringComparison.OrdinalIgnoreCase) >= 0) return "临时目录";
            return "未识别软件";
        }

        private void AppendOfficialJson(StringBuilder sb, List<SpaceDiagnosticItem> diagnostics)
        {
            sb.AppendLine("  \"official\": [");
            var rows = diagnostics.Where(d => d.ActionType == "官方清理" || d.ActionType == "禁止").OrderByDescending(d => d.Size).Take(3).ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var d = rows[i];
                sb.Append("    { \"label\": \"" + Json(d.Area) + "\", \"value\": \"" + Json(FileFinding.FormatBytes(d.Size)) + "\", \"hint\": \"" + Json(d.Recommendation) + "\" }");
                sb.AppendLine(i == rows.Count - 1 ? "" : ",");
            }
            sb.Append("  ]");
        }

        private void AppendQuarantineJson(StringBuilder sb)
        {
            sb.AppendLine("  \"quarantine\": [");
            var rows = LoadManifest()
                .GroupBy(q => string.IsNullOrWhiteSpace(q.BatchId) ? q.Id : q.BatchId)
                .Select(g => new
                {
                    Batch = g.Key,
                    Count = g.Count(),
                    Size = g.Sum(x => x.Size),
                    Time = g.Max(x => x.CleanedAt),
                    Expires = g.Max(x => x.ExpiresAt),
                    Source = string.Join("、", g.Select(x => ToZhSource(x.Source)).Distinct().Take(3).ToArray()),
                    Items = g.ToList()
                })
                .OrderByDescending(q => q.Time)
                .Take(30)
                .ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var q = rows[i];
                sb.Append("    { \"batch\": \"" + Json(q.Batch) + "\", \"count\": " + q.Count + ", \"source\": \"" + Json(q.Source) + "\", \"size\": \"" + Json(FileFinding.FormatBytes(q.Size)) + "\", \"time\": \"" + Json(q.Time.ToString("MM-dd HH:mm")) + "\", \"expires\": \"" + Json(q.Expires.ToString("MM-dd")) + "\", \"status\": \"可整批恢复\", \"ids\": [");
                for (int j = 0; j < q.Items.Count; j++)
                {
                    sb.Append("\"" + Json(q.Items[j].Id) + "\"");
                    if (j < q.Items.Count - 1) sb.Append(", ");
                }
                sb.Append("], \"paths\": [");
                for (int j = 0; j < q.Items.Take(8).Count(); j++)
                {
                    sb.Append("\"" + Json(q.Items[j].OriginalPath) + "\"");
                    if (j < q.Items.Take(8).Count() - 1) sb.Append(", ");
                }
                sb.Append("] }");
                sb.AppendLine(i == rows.Count - 1 ? "" : ",");
            }
            sb.Append("  ]");
        }

        private void AppendReportsJson(StringBuilder sb)
        {
            sb.AppendLine("  \"reports\": [");
            var reports = Directory.Exists(reportRoot)
                ? Directory.GetFiles(reportRoot, "clean-batch-*.txt").OrderByDescending(File.GetLastWriteTime).Take(8).ToList()
                : new List<string>();
            for (int i = 0; i < reports.Count; i++)
            {
                var file = new FileInfo(reports[i]);
                sb.Append("    { \"name\": \"" + Json(Path.GetFileName(file.FullName)) + "\", \"path\": \"" + Json(file.FullName) + "\", \"time\": \"" + Json(file.LastWriteTime.ToString("MM-dd HH:mm")) + "\", \"size\": \"" + Json(FileFinding.FormatBytes(file.Length)) + "\" }");
                sb.AppendLine(i == reports.Count - 1 ? "" : ",");
            }
            sb.Append("  ]");
        }

        private void AppendRulesJson(StringBuilder sb)
        {
            sb.AppendLine("  \"rules\": [");
            var whitelist = LoadWhitelist();
            int total = cleanerRules.Count + whitelist.Count;
            int emitted = 0;
            for (int i = 0; i < cleanerRules.Count; i++)
            {
                var r = cleanerRules[i];
                sb.Append("    { \"name\": \"" + Json(r.Name) + "\", \"match\": \"" + Json(r.PathContains) + "\", \"policy\": \"" + Json(ToZhRisk(r.Risk) + " / " + ToZhAction(r.Action)) + "\" }");
                emitted++;
                sb.AppendLine(emitted == total ? "" : ",");
            }
            foreach (string path in whitelist)
            {
                sb.Append("    { \"name\": \"白名单\", \"match\": \"" + Json(path) + "\", \"policy\": \"白名单 / 永不清理\" }");
                emitted++;
                sb.AppendLine(emitted == total ? "" : ",");
            }
            sb.Append("  ]");
        }

        private string Json(string value)
        {
            if (value == null) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private string Csv(string value)
        {
            value = value ?? "";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void CleanSelected()
        {
            var selectedIds = grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Cells["编号"].Value.ToString()).ToList();
            var candidates = findings.Where(f => selectedIds.Contains(f.Id) && f.RecommendedAction == "Clean" && f.Risk == "Low").ToList();
            if (candidates.Count == 0)
            {
                MessageBox.Show("请选择“低风险”且“建议操作”为“可清理”的项目。", "没有可清理项目", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            long total = candidates.Sum(f => f.Size);
            var confirm = MessageBox.Show(
                "将把 " + candidates.Count + " 个低风险文件移动到隔离区。\n预计释放: " + FileFinding.FormatBytes(total) + "\n\n继续吗？",
                "确认清理",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            var manifest = LoadManifest();
            int moved = 0;
            int failed = 0;
            foreach (var f in candidates)
            {
                if (IsProtectedPath(f.Path) || IsLocked(f.Path)) { failed++; continue; }
                try
                {
                    string sha256 = "";
                    try { sha256 = ComputeSha256(f.Path); } catch { }
                    string dest = Path.Combine(quarantineRoot, NewQuarantineName(f.Path));
                    if (File.Exists(dest)) dest = Path.Combine(quarantineRoot, Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(f.Path));
                    File.Move(f.Path, dest);
                    manifest.Add(new QuarantineItem
                    {
                        Id = Guid.NewGuid().ToString(),
                        OriginalPath = f.Path,
                        QuarantinePath = dest,
                        Size = f.Size,
                        Source = f.Source,
                        Risk = f.Risk,
                        CleanedAt = DateTime.Now,
                        ExpiresAt = DateTime.Now.AddDays(7),
                        Sha256 = sha256,
                        SnapshotReason = f.Reason
                    });
                    moved++;
                }
                catch { failed++; }
            }

            SaveManifest(manifest);
            findings.RemoveAll(f => candidates.Any(c => c.Id == f.Id) && !File.Exists(f.Path));
            BindGrid();
            UpdateSummary();
            MessageBox.Show("已移动到隔离区：" + moved + "\n已跳过或失败：" + failed, "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string NewQuarantineName(string path)
        {
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(path))).Replace("-", "").Substring(0, 16);
                string name = Path.GetFileName(path);
                foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                return hash + "-" + name;
            }
        }

        private List<QuarantineItem> LoadManifest()
        {
            var items = new List<QuarantineItem>();
            if (!File.Exists(manifestPath)) return items;
            foreach (var line in File.ReadAllLines(manifestPath, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(new[] { '\t' });
                if (parts.Length < 8) continue;
                long size;
                DateTime cleaned;
                DateTime expires;
                long.TryParse(parts[3], out size);
                DateTime.TryParse(parts[6], out cleaned);
                DateTime.TryParse(parts[7], out expires);
                items.Add(new QuarantineItem
                {
                    Id = parts[0],
                    BatchId = parts.Length > 10 ? parts[10] : parts[0],
                    OriginalPath = parts[1],
                    QuarantinePath = parts[2],
                    Size = size,
                    Source = parts[4],
                    Risk = parts[5],
                    CleanedAt = cleaned,
                    ExpiresAt = expires,
                    Sha256 = parts.Length > 8 ? parts[8] : "",
                    SnapshotReason = parts.Length > 9 ? parts[9] : ""
                });
            }
            return items;
        }

        private void SaveManifest(List<QuarantineItem> items)
        {
            var lines = items.Select(i => string.Join("\t", new[]
            {
                i.Id, i.OriginalPath, i.QuarantinePath, i.Size.ToString(), i.Source, i.Risk,
                i.CleanedAt.ToString("s"), i.ExpiresAt.ToString("s"), i.Sha256 ?? "", (i.SnapshotReason ?? "").Replace("\t", " "), string.IsNullOrWhiteSpace(i.BatchId) ? i.Id : i.BatchId
            }));
            File.WriteAllLines(manifestPath, lines, Encoding.UTF8);
        }

        private void ShowQuarantine()
        {
            using (var form = new Form())
            {
                form.Text = "隔离区";
                form.Size = new Size(920, 480);
                form.StartPosition = FormStartPosition.CenterParent;
                var qGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
                var restore = new Button { Text = "恢复选中项", Dock = DockStyle.Bottom, Height = 38 };
                var items = LoadManifest();
                qGrid.DataSource = items.Select(i => new
                {
                    编号 = i.Id,
                    来源 = ToZhSource(i.Source),
                    大小 = FileFinding.FormatBytes(i.Size),
                    风险 = ToZhRisk(i.Risk),
                    到期时间 = i.ExpiresAt,
                    SHA256 = string.IsNullOrWhiteSpace(i.Sha256) ? "-" : i.Sha256,
                    原路径 = i.OriginalPath
                }).ToList();
                restore.Click += (s, e) =>
                {
                    if (qGrid.SelectedRows.Count == 0) return;
                    string id = qGrid.SelectedRows[0].Cells["编号"].Value.ToString();
                    RestoreItem(id);
                    form.Close();
                };
                form.Controls.Add(qGrid);
                form.Controls.Add(restore);
                form.ShowDialog(this);
            }
        }

        private void ShowSpaceDiagnosis(List<SpaceDiagnosticItem> items)
        {
            using (var form = new Form())
            {
                form.Text = "C 盘空间诊断";
                form.Size = new Size(1180, 700);
                form.StartPosition = FormStartPosition.CenterParent;
                form.Font = new Font("Microsoft YaHei UI", 9F);

                var root = new TableLayoutPanel();
                root.Dock = DockStyle.Fill;
                root.RowCount = 3;
                root.ColumnCount = 1;
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
                form.Controls.Add(root);

                long total = items.Sum(i => i.Size);
                var header = new Label
                {
                    Dock = DockStyle.Fill,
                    Text = "空间诊断只分析占用和处理建议，不会删除文件。重点关注：迁移、官方清理、专项清理，而不是直接删除。    当前重点项合计：" + FileFinding.FormatBytes(total),
                    Padding = new Padding(14, 14, 14, 8),
                    BackColor = Color.FromArgb(246, 248, 250)
                };
                root.Controls.Add(header, 0, 0);

                var diagGrid = new DataGridView();
                diagGrid.Dock = DockStyle.Fill;
                diagGrid.ReadOnly = true;
                diagGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                diagGrid.MultiSelect = false;
                diagGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                diagGrid.RowHeadersVisible = false;
                diagGrid.BackgroundColor = Color.White;
                diagGrid.DataSource = items.Select(i => new
                {
                    区域 = i.Area,
                    大小 = i.SizeText,
                    建议动作 = i.ActionType,
                    风险 = i.Risk,
                    建议 = i.Recommendation,
                    路径 = i.Path
                }).ToList();
                if (diagGrid.Columns["路径"] != null) diagGrid.Columns["路径"].FillWeight = 260;
                if (diagGrid.Columns["建议"] != null) diagGrid.Columns["建议"].FillWeight = 150;
                root.Controls.Add(diagGrid, 0, 1);

                var bottom = new TableLayoutPanel();
                bottom.Dock = DockStyle.Fill;
                bottom.ColumnCount = 2;
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78));
                bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
                bottom.Padding = new Padding(14, 8, 14, 10);
                bottom.BackColor = Color.FromArgb(246, 248, 250);
                root.Controls.Add(bottom, 0, 2);

                var info = new TextBox();
                info.Dock = DockStyle.Fill;
                info.Multiline = true;
                info.ReadOnly = true;
                info.BackColor = Color.White;
                info.Text = "处理原则：" + Environment.NewLine +
                    "1. 用户文件优先迁移，不自动删除。" + Environment.NewLine +
                    "2. Windows、WinSxS、Installer 走系统官方清理，不手动删除。" + Environment.NewLine +
                    "3. Docker、npm、pip、Gradle 等使用专项清理命令。" + Environment.NewLine +
                    "4. AppData 和 ProgramData 需要识别软件归属后再处理。";
                bottom.Controls.Add(info, 0, 0);

                var buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Fill;
                buttons.FlowDirection = FlowDirection.TopDown;
                var export = new Button { Text = "导出诊断报告", Width = 150, Height = 32 };
                var close = new Button { Text = "关闭", Width = 150, Height = 32 };
                export.Click += (s, e) =>
                {
                    string path = Path.Combine(reportRoot, "space-diagnosis-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
                    SaveSpaceDiagnosisCsv(path, items);
                    MessageBox.Show("诊断报告已导出：\n" + path, "导出诊断报告", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                close.Click += (s, e) => form.Close();
                buttons.Controls.Add(export);
                buttons.Controls.Add(close);
                bottom.Controls.Add(buttons, 1, 0);

                form.ShowDialog(this);
            }
        }

        private void ShowSpaceOverview(List<SpaceDiagnosticItem> items)
        {
            using (var form = new Form())
            {
                form.Text = "C 盘空间总览";
                form.Size = new Size(980, 620);
                form.StartPosition = FormStartPosition.CenterParent;
                form.Font = new Font("Microsoft YaHei UI", 9F);

                var root = new TableLayoutPanel();
                root.Dock = DockStyle.Fill;
                root.RowCount = 3;
                root.ColumnCount = 1;
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
                form.Controls.Add(root);

                string drive = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
                DriveInfo driveInfo = new DriveInfo(drive + "\\");
                var header = new Label
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(14, 12, 14, 8),
                    BackColor = Color.FromArgb(246, 248, 250),
                    Text = string.Format("系统盘 {0}    总容量：{1}    可用：{2}    已用：{3}",
                        driveInfo.Name,
                        FileFinding.FormatBytes(driveInfo.TotalSize),
                        FileFinding.FormatBytes(driveInfo.AvailableFreeSpace),
                        FileFinding.FormatBytes(driveInfo.TotalSize - driveInfo.AvailableFreeSpace))
                };
                root.Controls.Add(header, 0, 0);

                var summary = items
                    .GroupBy(i => i.ActionType)
                    .Select(g => new
                    {
                        处理方式 = g.Key,
                        Bytes = g.Sum(x => x.Size),
                        估算空间 = FileFinding.FormatBytes(g.Sum(x => x.Size)),
                        项目数 = g.Count(),
                        说明 = GetActionDescription(g.Key)
                    })
                    .OrderByDescending(x => x.Bytes)
                    .Select(x => new { x.处理方式, x.估算空间, x.项目数, x.说明 })
                    .ToList();

                var gridOverview = new DataGridView();
                gridOverview.Dock = DockStyle.Fill;
                gridOverview.ReadOnly = true;
                gridOverview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                gridOverview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                gridOverview.RowHeadersVisible = false;
                gridOverview.BackgroundColor = Color.White;
                gridOverview.DataSource = summary;
                root.Controls.Add(gridOverview, 0, 1);

                var buttons = new FlowLayoutPanel();
                buttons.Dock = DockStyle.Fill;
                buttons.Padding = new Padding(14, 10, 14, 10);
                buttons.BackColor = Color.FromArgb(246, 248, 250);
                var openStorage = new Button { Text = "打开存储设置", Width = 130, Height = 34 };
                var openCleanMgr = new Button { Text = "打开磁盘清理", Width = 130, Height = 34 };
                var showDism = new Button { Text = "查看 DISM 命令", Width = 140, Height = 34 };
                var openRules = new Button { Text = "打开规则库", Width = 120, Height = 34 };
                var openDiagnosis = new Button { Text = "查看详细诊断", Width = 140, Height = 34 };
                openStorage.Click += (s, e) => StartProcessSafe("ms-settings:storagesense", "");
                openCleanMgr.Click += (s, e) => StartProcessSafe("cleanmgr.exe", "");
                showDism.Click += (s, e) => MessageBox.Show(
                    "管理员 PowerShell 中可执行：\n\nDISM /Online /Cleanup-Image /StartComponentCleanup\n\n注意：WinSxS 组件存储不要手动删除。",
                    "Windows 组件官方清理",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                openRules.Click += (s, e) => StartProcessSafe("notepad.exe", "\"" + rulesPath + "\"");
                openDiagnosis.Click += (s, e) => ShowSpaceDiagnosis(items);
                buttons.Controls.Add(openStorage);
                buttons.Controls.Add(openCleanMgr);
                buttons.Controls.Add(showDism);
                buttons.Controls.Add(openRules);
                buttons.Controls.Add(openDiagnosis);
                root.Controls.Add(buttons, 0, 2);

                form.ShowDialog(this);
            }
        }

        private string GetActionDescription(string action)
        {
            if (action == "清理") return "低风险项目，可在确认后清理。";
            if (action == "迁移") return "用户数据或大文件，建议移动到其他磁盘。";
            if (action == "官方清理") return "Windows 相关内容，应使用系统工具处理。";
            if (action == "专项清理") return "开发工具、容器或软件缓存，应使用对应工具命令。";
            if (action == "卸载") return "软件安装目录，应通过设置或控制面板卸载。";
            if (action == "禁止") return "系统关键目录或安装缓存，不应手动删除。";
            if (action == "保留") return "配置、Profile、数据库或存档，默认保留。";
            return "需要进一步确认。";
        }

        private void StartProcessSafe(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo();
                psi.FileName = fileName;
                psi.Arguments = arguments;
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "无法打开", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveSpaceDiagnosisCsv(string path, IEnumerable<SpaceDiagnosticItem> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Area,Path,Size,SizeText,ActionType,Risk,Recommendation,Reason");
            foreach (var i in rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(i.Area), Csv(i.Path), i.Size.ToString(), Csv(i.SizeText), Csv(i.ActionType),
                    Csv(i.Risk), Csv(i.Recommendation), Csv(i.Reason)
                }));
            }
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private void RestoreItem(string id)
        {
            var items = LoadManifest();
            var item = items.FirstOrDefault(i => i.Id == id);
            if (item == null) return;
            if (!File.Exists(item.QuarantinePath))
            {
                MessageBox.Show("隔离文件不存在。", "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(item.OriginalPath));
            File.Move(item.QuarantinePath, item.OriginalPath);
            items.RemoveAll(i => i.Id == id);
            SaveManifest(items);
            MessageBox.Show("已恢复：\n" + item.OriginalPath, "恢复完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void RestoreItemWithoutUi(string id)
        {
            var items = LoadManifest();
            var item = items.FirstOrDefault(i => i.Id == id);
            if (item == null) throw new InvalidOperationException("Quarantine item not found: " + id);
            if (!File.Exists(item.QuarantinePath)) throw new FileNotFoundException("Quarantine file missing", item.QuarantinePath);
            Directory.CreateDirectory(Path.GetDirectoryName(item.OriginalPath));
            if (File.Exists(item.OriginalPath)) File.Delete(item.OriginalPath);
            File.Move(item.QuarantinePath, item.OriginalPath);
            items.RemoveAll(i => i.Id == id);
            SaveManifest(items);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args != null && args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                using (var form = new MainForm())
                {
                    Console.WriteLine(form.RunSelfTest());
                }
                return;
            }

            int exportIndex = args == null ? -1 : Array.FindIndex(args, a => string.Equals(a, "--export-json", StringComparison.OrdinalIgnoreCase));
            if (exportIndex >= 0 && args.Length > exportIndex + 1)
            {
                string outputPath = args[exportIndex + 1];
                string mode = "Quick";
                int limit = 3000;
                int modeIndex = Array.FindIndex(args, a => string.Equals(a, "--mode", StringComparison.OrdinalIgnoreCase));
                if (modeIndex >= 0 && args.Length > modeIndex + 1) mode = args[modeIndex + 1];
                int maxIndex = Array.FindIndex(args, a => string.Equals(a, "--max-files", StringComparison.OrdinalIgnoreCase));
                if (maxIndex >= 0 && args.Length > maxIndex + 1) int.TryParse(args[maxIndex + 1], out limit);
                if (limit <= 0) limit = 3000;

                using (var form = new MainForm())
                {
                    Console.WriteLine(form.ExportModernUiJson(outputPath, mode, limit));
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}

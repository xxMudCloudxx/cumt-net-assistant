using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using AutoUpdaterDotNET;

namespace CampusNetAssistant
{
    public class MainForm : Form
    {
        // ══════════════ 颜色主题 ══════════════
        private static readonly Color Primary       = Color.FromArgb(99, 102, 241);   // Indigo 500
        private static readonly Color PrimaryDark   = Color.FromArgb(79, 70, 229);    // Indigo 600
        private static readonly Color Danger        = Color.FromArgb(244, 63, 94);    // Rose 500
        private static readonly Color DangerDark    = Color.FromArgb(225, 29, 72);    // Rose 600
        private static readonly Color Success       = Color.FromArgb(16, 185, 129);   // Emerald 500
        private static readonly Color Warning       = Color.FromArgb(245, 158, 11);   // Amber 500
        private static readonly Color BgColor       = Color.FromArgb(248, 250, 252);  // Slate 50
        private static readonly Color CardBg        = Color.White;
        private static readonly Color HeaderStart   = Color.FromArgb(56, 189, 248);   // Sky 400
        private static readonly Color HeaderEnd     = Color.FromArgb(99, 102, 241);   // Indigo 500
        private static readonly Color TextDark      = Color.FromArgb(15, 23, 42);     // Slate 900
        private static readonly Color TextMuted     = Color.FromArgb(100, 116, 139);  // Slate 500
        private static readonly Color BorderClr     = Color.FromArgb(226, 232, 240);  // Slate 200

        // ══════════════ 控件 ══════════════
        private NotifyIcon   _trayIcon   = null!;
        private ContextMenuStrip _trayMenu = null!;

        private TextBox   _txtStudentId  = null!;
        private TextBox   _txtPassword   = null!;
        private ComboBox  _cboOperator   = null!;
        private ComboBox  _cboAdapter    = null!;
        private CheckBox  _chkAutoStart  = null!;
        private CheckBox  _chkAutoLogin  = null!;
        private Button    _btnLogin      = null!;
        private Button    _btnLogout     = null!;
        private Button    _btnToggle     = null!;
        private Button    _btnRefresh    = null!;
        private Button    _btnCheckUpdate = null!;
        private Button    _btnAbout      = null!;
        private Label     _lblStatus     = null!;

        // ══════════════ 业务 ══════════════
        private readonly NetworkMonitor _monitor = new();
        private AppConfig _config = new();
        private bool _adapterDisabled = false;
        private bool _firstShow = true;
        private bool _isManualUpdateCheck = false;

        // ══════════════ 构造 ══════════════
        public MainForm()
        {
            BuildUI();
            BuildTray();
            LoadConfig();

            // 网络守护事件绑定
            _monitor.StatusChanged    += msg => Invoke(() => SetStatus(msg, Warning));
            _monitor.ReloginRequested += AutoLoginAsync;

            // ── 更新检查事件绑定 ──
            AutoUpdater.CheckForUpdateEvent += OnUpdateCheckComplete;

            if (_config.AutoLogin && !string.IsNullOrEmpty(_config.StudentId))
            {
                _ = DoLoginAsync(silent: false);
                _monitor.Start();
            }

            // ── 自动检查更新 ──
            CheckForUpdates();
        }

        private void OnUpdateCheckComplete(UpdateInfoEventArgs args)
        {
            if (args.Error == null)
            {
                if (!args.IsUpdateAvailable && _isManualUpdateCheck)
                {
                    // 只在手动检查时显示"已是最新版本"提示
                    var versionMatch = Regex.Match(args.InstalledVersion.ToString(), "\\d+(?:\\.\\d+){1,3}");
                    var displayVersion = versionMatch.Success ? versionMatch.Value : args.InstalledVersion.ToString();
                    MessageBox.Show(
                        $"当前已是最新版本 v{displayVersion}",
                        "检查更新",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                // 有新版本时由AutoUpdater自动显示对话框
            }
            else if (_isManualUpdateCheck)
            {
                // 只在手动检查时显示错误提示
                var errorMsg = args.Error.Message;
                var friendlyMsg = "";
                
                // 针对常见错误提供友好提示
                var innerMsg = args.Error.InnerException?.Message ?? "";
                if (errorMsg.Contains("SSL") || errorMsg.Contains("TLS") || 
                    innerMsg.Contains("SSL") || innerMsg.Contains("TLS") ||
                    errorMsg.Contains("established") || innerMsg.Contains("established"))
                {
                    friendlyMsg = "无法与更新服务器建立安全连接（SSL/TLS 失败）。\n\n" +
                                 "这通常是因为网络环境限制了 HTTPS 访问。\n" +
                                 "请检查网络连接或代理设置。\n\n" +
                                 "是否访问 GitHub Releases 页面手动检查更新？";
                }
                else if (errorMsg.Contains("non-existing field") || errorMsg.Contains("字段") || 
                         errorMsg.Contains("MissingField"))
                {
                    friendlyMsg = "更新服务器返回了非预期内容（可能是网络拦截导致）。\n\n" +
                                 "这可能是因为：\n" +
                                 "1. 校园网未登录（网页认证拦截）\n" +
                                 "2. 使用了 Watt Toolkit (Steam++) 或其他代理软件\n\n" +
                                 "是否访问 GitHub Releases 页面手动检查更新？";
                }
                else if (errorMsg.Contains("远程名称无法解析") || errorMsg.Contains("network") || 
                         errorMsg.Contains("连接") || errorMsg.Contains("timed out"))
                {
                    friendlyMsg = "网络连接失败，无法访问更新服务器。\n\n是否访问 GitHub Releases 页面手动检查更新？";
                }
                else
                {
                    friendlyMsg = $"检查更新失败：{errorMsg}\n\n是否访问 GitHub Releases 页面手动检查更新？";
                }
                
                var result = MessageBox.Show(
                    friendlyMsg,
                    "检查更新",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );
                
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://github.com/xxMudCloudxx/cumt-campus-ant/releases",
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
            
            _isManualUpdateCheck = false; // 重置标志
        }

        private void CheckForUpdates()
        {
            // 兼容语义化版本后缀（如 1.0.4-local / 1.0.4+gitsha），提取纯数字版本部分
            var rawVersion = Application.ProductVersion;
            Version? installedVersion;

            if (!Version.TryParse(rawVersion, out installedVersion))
            {
                var match = Regex.Match(rawVersion, @"\d+(?:\.\d+){1,3}");
                if (!match.Success || !Version.TryParse(match.Value, out installedVersion))
                {
                    installedVersion = new Version(1, 0, 0, 0);
                }
            }

            AutoUpdater.InstalledVersion = installedVersion ?? new Version(1, 0, 0, 0);
            AutoUpdater.ShowSkipButton = true;
            AutoUpdater.ShowRemindLaterButton = true;
            AutoUpdater.RunUpdateAsAdmin = false;
            
            // 添加错误处理，避免因网络或服务器问题导致程序异常
            try
            {
                // 使用 jsDelivr CDN 加速，避免直连 GitHub 被 GFW/SNI 阻断
                AutoUpdater.Start("https://cdn.jsdelivr.net/gh/xxMudCloudxx/cumt-campus-ant@main/update.xml");
            }
            catch (Exception ex)
            {
                // 更新检查失败时静默处理，不影响主程序
                if (_isManualUpdateCheck)
                {
                    MessageBox.Show(
                        $"无法连接到更新服务器：{ex.Message}",
                        "检查更新",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
        }

        private void CheckForUpdatesManually()
        {
            _isManualUpdateCheck = true;
            
            // 检查是否是本地测试/开发版本（只警告预发布版本，不警告构建元数据）
            var rawVersion = Application.ProductVersion;
            
            // 只对带 - 前缀的预发布版本（如 1.0.6-beta）发出警告
            // + 后缀是构建元数据（如 1.0.6+commit_hash），提取后是合法的正式版本，不需要警告
            if (rawVersion.Contains("-"))
            {
                var result = MessageBox.Show(
                    $"当前版本 ({rawVersion}) 是预发布/测试版本。\n\n" +
                    "更新检查可能会失败，因为该版本尚未正式发布到 GitHub。\n\n" +
                    "是否仍要继续检查更新？",
                    "检查更新",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );
                
                if (result == DialogResult.No)
                {
                    _isManualUpdateCheck = false;
                    return;
                }
            }
            
            CheckForUpdates();
        }

        // ── 仅在自动登录已配置时隐藏窗体到托盘 ──
        protected override void SetVisibleCore(bool value)
        {
            if (_firstShow)
            {
                _firstShow = false;
                // 已配置自动登录时才隐藏到托盘，否则正常显示主窗口
                if (_config.AutoLogin && !string.IsNullOrEmpty(_config.StudentId))
                {
                    base.SetVisibleCore(false);
                    return;
                }
            }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            _monitor.Dispose();
            _trayIcon.Visible = false;
            base.OnFormClosing(e);
        }

        // ══════════════════════════════════════
        //  系统托盘
        // ══════════════════════════════════════
        private void BuildTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("🏠 打开主面板",      null, (_, _) => ShowForm());
            _trayMenu.Items.Add("🚀 立即登录",        null, async (_, _) => await DoLoginAsync());
            _trayMenu.Items.Add("⛔ 断开校园网",      null, async (_, _) => await DoLogoutAsync());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("🔌 禁用/启用以太网", null, (_, _) => ToggleAdapter());
            _trayMenu.Items.Add("🔄 检查更新",        null, (_, _) => CheckForUpdatesManually());
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("❌ 退出", null, (_, _) =>
            {
                _trayIcon.Visible = false;
                _monitor.Dispose();
                Application.Exit();
            });

            // 美化右键菜单
            _trayMenu.Font = new Font("Microsoft YaHei UI", 9.5f);
            _trayMenu.ShowImageMargin = false;
            _trayMenu.BackColor = Color.White;
            _trayMenu.Renderer = new ToolStripProfessionalRenderer(new ModernColorTable());

            _trayIcon = new NotifyIcon
            {
                Text             = "CUMT校园网助手",
                Icon             = CreateTrayIcon(),
                ContextMenuStrip = _trayMenu,
                Visible          = true
            };
            // 左键单击显示主窗口，右键显示菜单
            _trayIcon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left)
                    ShowForm();
            };
        }

        private Icon CreateTrayIcon()
        {
            try
            {
                // 优先使用 EXE 内嵌图标
                var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                    return new Icon(icon, 64, 64);
            }
            catch { }

            // 回退：程序化绘制网络图标
            var bmp = new Bitmap(64, 64);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Primary);
            g.FillEllipse(brush, 2, 2, 60, 60);
            using var font = new Font("Microsoft YaHei", 32f, FontStyle.Bold);
            TextRenderer.DrawText(g, "C", font, new Rectangle(0, 0, 64, 64), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return Icon.FromHandle(bmp.GetHicon());
        }

        private class ModernColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(243, 244, 246);
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuBorder => Color.FromArgb(209, 213, 219);
            public override Color ToolStripDropDownBackground => Color.White;
            public override Color ImageMarginGradientBegin => Color.White;
            public override Color ImageMarginGradientMiddle => Color.White;
            public override Color ImageMarginGradientEnd => Color.White;
            public override Color SeparatorDark => Color.FromArgb(229, 231, 235);
            public override Color SeparatorLight => Color.White;
        }

        private void ShowForm()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        // ══════════════════════════════════════
        //  核心业务
        // ══════════════════════════════════════
        private async Task DoLoginAsync(bool silent = false)
        {
            SetStatus("正在登录校园网…", Warning);
            var op = (OperatorType)_cboOperator.SelectedIndex;
            string pwd = _txtPassword.Text.Trim();
            string uid = _txtStudentId.Text.Trim();

            if (string.IsNullOrEmpty(uid) || string.IsNullOrEmpty(pwd))
            {
                SetStatus("请输入学号和密码", Danger);
                return;
            }

            var result = await LoginService.LoginAsync(uid, pwd, op);

            if (result.Success)
            {
                SetStatus(result.Message, Success);
                _monitor.ResetFailures();
                ShowBalloon("登录成功", result.Message, ToolTipIcon.Info);
            }
            else
            {
                SetStatus(result.Message, Danger);
                _monitor.RecordFailure();
                ShowBalloon("登录失败", result.Message, ToolTipIcon.Warning);
            }
        }

        private async Task AutoLoginAsync()
        {
            await Task.Run(async () =>
            {
                var result = await LoginService.LoginAsync(
                    _config.StudentId,
                    ConfigManager.DecryptPassword(_config.EncryptedPassword),
                    (OperatorType)_config.OperatorIndex);

                Invoke(() =>
                {
                    if (result.Success)
                    {
                        SetStatus(result.Message, Success);
                        _monitor.ResetFailures();
                        ShowBalloon("自动登录成功", result.Message, ToolTipIcon.Info);
                    }
                    else
                    {
                        SetStatus(result.Message, Danger);
                        _monitor.RecordFailure();
                        ShowBalloon("自动登录失败", result.Message, ToolTipIcon.Warning);
                    }
                });
            });
        }

        private async Task DoLogoutAsync()
        {
            SetStatus("正在断开校园网…", Warning);
            var result = await LoginService.LogoutAsync();
            if (result.Success)
            {
                SetStatus(result.Message, Success);
                ShowBalloon("已断开", result.Message, ToolTipIcon.Info);
            }
            else
            {
                SetStatus(result.Message, Danger);
                ShowBalloon("断开失败", result.Message, ToolTipIcon.Warning);
            }
        }

        private void ToggleAdapter()
        {
            string name = _cboAdapter.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                ShowBalloon("提示", "请先选择要操作的网络适配器", ToolTipIcon.Warning);
                return;
            }
            _adapterDisabled = !_adapterDisabled;
            bool ok = AdapterHelper.SetAdapterState(name, !_adapterDisabled);
            if (ok)
            {
                string state = _adapterDisabled ? "已禁用" : "已启用";
                _btnToggle.Text = _adapterDisabled ? "🔌 启用网卡" : "🔌 禁用网卡";
                SetStatus($"适配器 [{name}] {state}", _adapterDisabled ? Warning : Success);
                ShowBalloon("网卡操作", $"适配器 [{name}] {state}", ToolTipIcon.Info);
            }
            else
            {
                _adapterDisabled = !_adapterDisabled; // 回滚
                SetStatus("操作失败（可能已取消 UAC 授权）", Danger);
            }
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            _trayIcon.ShowBalloonTip(3000, title, text, icon);
        }

        // ══════════════════════════════════════
        //  配置读写
        // ══════════════════════════════════════
        private void LoadConfig()
        {
            _config = ConfigManager.Load();
            _txtStudentId.Text         = _config.StudentId;
            _txtPassword.Text          = ConfigManager.DecryptPassword(_config.EncryptedPassword);
            _cboOperator.SelectedIndex = Math.Clamp(_config.OperatorIndex, 0, 3);
            _chkAutoStart.Checked      = _config.AutoStart;
            _chkAutoLogin.Checked      = _config.AutoLogin;

            RefreshAdapters();
            if (!string.IsNullOrEmpty(_config.SelectedAdapter))
            {
                int idx = _cboAdapter.Items.IndexOf(_config.SelectedAdapter);
                if (idx >= 0) _cboAdapter.SelectedIndex = idx;
            }
        }

        private void SaveConfig()
        {
            _config.StudentId         = _txtStudentId.Text.Trim();
            _config.EncryptedPassword = ConfigManager.EncryptPassword(_txtPassword.Text.Trim());
            _config.OperatorIndex     = _cboOperator.SelectedIndex;
            _config.SelectedAdapter   = _cboAdapter.SelectedItem?.ToString() ?? "";
            _config.AutoStart         = _chkAutoStart.Checked;
            _config.AutoLogin         = _chkAutoLogin.Checked;

            ConfigManager.Save(_config);
            ConfigManager.SetAutoStart(_config.AutoStart);

            if (_config.AutoLogin)
                _monitor.Start();
            else
                _monitor.Stop();
        }

        private void RefreshAdapters()
        {
            _cboAdapter.Items.Clear();
            foreach (var name in AdapterHelper.GetAllAdapters())
                _cboAdapter.Items.Add(name);
            if (_cboAdapter.Items.Count > 0)
                _cboAdapter.SelectedIndex = 0;
        }

        // ══════════════════════════════════════
        //  现代化 UI 构建
        // ══════════════════════════════════════
        private void BuildUI()
        {
            // ── 窗体基本属性 ──
            Text            = "CUMT校园网助手";
            Size            = new Size(460, 720);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;
            StartPosition   = FormStartPosition.CenterScreen;
            BackColor       = BgColor;
            Font            = new Font("Microsoft YaHei UI", 10f);

            // ── 渐变头部面板 ──
            var header = new Panel { Dock = DockStyle.Top, Height = 100 };
            header.Paint += (s, e) =>
            {
                using var brush = new LinearGradientBrush(
                    header.ClientRectangle, HeaderStart, HeaderEnd, 45f);
                e.Graphics.FillRectangle(brush, header.ClientRectangle);

                using var titleFont = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold);
                TextRenderer.DrawText(e.Graphics, "CUMT校园网助手", titleFont,
                    new Point(24, 22), Color.White);

                using var subFont = new Font("Microsoft YaHei UI", 9.5f);
                TextRenderer.DrawText(e.Graphics, "⚡ 轻量 · 高效 · 自动", subFont,
                    new Point(28, 62), Color.FromArgb(220, 255, 255, 255));
            };
            Controls.Add(header);

            // ── 主内容区 ──
            var body = new Panel
            {
                Location = new Point(0, 100),
                Size     = new Size(460, 600),
                Padding  = new Padding(24, 20, 24, 20)
            };
            Controls.Add(body);

            int y = 16;

            // ── 账号信息卡片 ──
            var card1 = MakeCard(body, "账号信息", ref y, 190);
            int cy = 44;
            MakeLabel(card1,   "学号", 20, cy);
            _txtStudentId = MakeTextBox(card1, 90, cy, 280); cy += 44;
            MakeLabel(card1,   "密码", 20, cy);
            _txtPassword  = MakeTextBox(card1, 90, cy, 280, isPassword: true); cy += 44;
            MakeLabel(card1,   "运营商", 20, cy);
            _cboOperator  = MakeComboBox(card1, 90, cy, 280,
                new[] { "校园网", "中国电信", "中国联通", "中国移动" });

            y += 16;

            // ── 网络设置卡片 ──
            var card2 = MakeCard(body, "网络设置", ref y, 145);
            cy = 44;
            MakeLabel(card2, "适配器", 20, cy);
            _cboAdapter = MakeComboBox(card2, 90, cy, 218);
            _btnRefresh = MakeCircularIconButton(card2, "⟳", 318, cy - 2, 34, 15f);
            _btnRefresh.Click += (_, _) => RefreshAdapters();
            cy += 48;
            _chkAutoStart = MakeCheckBox(card2, "开机自启",  20, cy);
            _chkAutoLogin = MakeCheckBox(card2, "自动登录", 160, cy);

            y += 24;

            // ── 操作按钮 ──
            _btnLogin = MakeButton(body, "🔐 保存并登录", 26, y, 196, 48, Primary, PrimaryDark);
            _btnLogin.Click += async (_, _) => { SaveConfig(); await DoLoginAsync(); };

            _btnLogout = MakeButton(body, "⛔ 断开校园网", 232, y, 196, 48, Danger, DangerDark);
            _btnLogout.Click += async (_, _) => await DoLogoutAsync();

            y += 64;

            _btnToggle = MakeButton(body, "🔌 禁用网卡", 26, y, 196, 48,
                Color.FromArgb(148, 163, 184), Color.FromArgb(100, 116, 139));
            _btnToggle.Click += (_, _) => ToggleAdapter();

            _btnCheckUpdate = MakeButton(body, "🔄 检查更新", 232, y, 196, 48,
                Color.FromArgb(56, 189, 248), Color.FromArgb(2, 132, 199));
            _btnCheckUpdate.Click += (_, _) => CheckForUpdatesManually();

            y += 76;

            // ── 状态栏 ──
            _lblStatus = new Label
            {
                Text      = "就绪",
                Location  = new Point(26, y),
                Size      = new Size(330, 24),
                ForeColor = TextMuted,
                Font      = new Font("Microsoft YaHei UI", 9.5f),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            body.Controls.Add(_lblStatus);

            // ── 关于按钮 ──
            _btnAbout = MakeShadowIconButton(body, "?", 392, y - 6, 36, 14f);
            _btnAbout.Click += (_, _) => ShowAboutDialog();
        }

        private void SetStatus(string text, Color color)
        {
            if (InvokeRequired) { Invoke(() => SetStatus(text, color)); return; }
            _lblStatus.Text      = text;
            _lblStatus.ForeColor = color;
        }

        private void ShowAboutDialog()
        {
            var aboutForm = new Form
            {
                Text            = "关于",
                Size            = new Size(400, 320),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox     = false,
                MinimizeBox     = false,
                StartPosition   = FormStartPosition.CenterParent,
                BackColor       = Color.White,
                Font            = new Font("Microsoft YaHei UI", 9.5f)
            };

            int y = 20;

            // 标题
            var lblTitle = new Label
            {
                Text      = "CUMT校园网助手",
                Location  = new Point(0, y),
                Size      = new Size(400, 30),
                Font      = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold),
                ForeColor = Primary,
                TextAlign = ContentAlignment.MiddleCenter
            };
            aboutForm.Controls.Add(lblTitle);
            y += 40;

            // 版本（只显示数字部分）
            var versionMatch = Regex.Match(Application.ProductVersion, "\\d+(?:\\.\\d+){1,3}");
            var displayVersion = versionMatch.Success ? versionMatch.Value : Application.ProductVersion;
            var lblVersion = new Label
            {
                Text      = $"版本 {displayVersion}",
                Location  = new Point(0, y),
                Size      = new Size(400, 20),
                ForeColor = TextMuted,
                TextAlign = ContentAlignment.MiddleCenter
            };
            aboutForm.Controls.Add(lblVersion);
            y += 35;

            // 作者信息
            AddInfoRow(aboutForm, "作者", "谷粒多 (xxMudCloudxx)", ref y);
            AddInfoRow(aboutForm, "QQ", "2597453011", ref y);
            
            // GitHub 链接 (可点击)
            var lblGithubLabel = new Label
            {
                Text      = "GitHub",
                Location  = new Point(60, y),
                Size      = new Size(80, 20),
                ForeColor = TextDark,
                Font      = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold)
            };
            aboutForm.Controls.Add(lblGithubLabel);

            var lblGithub = new LinkLabel
            {
                Text          = "github.com/xxMudCloudxx/cumt-campus-ant",
                Location      = new Point(145, y),
                Size          = new Size(200, 20),
                LinkColor     = Primary,
                ActiveLinkColor = PrimaryDark,
                VisitedLinkColor = Primary,
                Font          = new Font("Microsoft YaHei UI", 9f),
                LinkBehavior  = LinkBehavior.HoverUnderline
            };
            lblGithub.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://github.com/xxMudCloudxx/cumt-campus-ant",
                        UseShellExecute = true
                    });
                }
                catch { }
            };
            aboutForm.Controls.Add(lblGithub);
            y += 35;

            // 版权信息
            var lblCopyright = new Label
            {
                Text      = "Copyright © 2026 谷粒多",
                Location  = new Point(0, y),
                Size      = new Size(400, 20),
                ForeColor = TextMuted,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Microsoft YaHei UI", 8.5f)
            };
            aboutForm.Controls.Add(lblCopyright);
            y += 30;

            // 关闭按钮
            var btnClose = new Button
            {
                Text      = "确定",
                Location  = new Point(150, y),
                Size      = new Size(100, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Primary,
                ForeColor = Color.White,
                Font      = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => aboutForm.Close();
            aboutForm.Controls.Add(btnClose);

            aboutForm.ShowDialog(this);
        }

        private void AddInfoRow(Form form, string label, string value, ref int y)
        {
            var lblLabel = new Label
            {
                Text      = label,
                Location  = new Point(60, y),
                Size      = new Size(80, 20),
                ForeColor = TextDark,
                Font      = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold)
            };
            form.Controls.Add(lblLabel);

            var lblValue = new Label
            {
                Text      = value,
                Location  = new Point(145, y),
                Size      = new Size(200, 20),
                ForeColor = TextMuted
            };
            form.Controls.Add(lblValue);

            y += 25;
        }

        // ══════════════ UI 辅助方法 ══════════════

        private Panel MakeCard(Control parent, string title, ref int y, int height)
        {
            var card = new Panel
            {
                Location    = new Point(24, y),
                Size        = new Size(404, height),
                BackColor   = Color.Transparent, 
            };
            
            var innerCard = new Panel
            {
                Location  = new Point(0, 0),
                Size      = new Size(404, height),
                BackColor = Color.Transparent,
            };

            innerCard.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using var path = RoundedRect(new Rectangle(0, 0, innerCard.Width - 1, innerCard.Height - 1), 12);
                
                using var fill = new SolidBrush(CardBg);
                g.FillPath(fill, path);

                using var pen = new Pen(BorderClr, 1f);
                g.DrawPath(pen, path);

                using var font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
                TextRenderer.DrawText(g, title, font, new Point(20, 14), PrimaryDark);
                
                using var sepPen = new Pen(Color.FromArgb(241, 245, 249), 1f);
                g.DrawLine(sepPen, 20, 40, innerCard.Width - 20, 40);
            };
            card.Controls.Add(innerCard);
            parent.Controls.Add(card);
            y += height;
            return innerCard;
        }

        private Label MakeLabel(Control parent, string text, int x, int y)
        {
            var lbl = new Label
            {
                Text      = text,
                Location  = new Point(x, y + 4),
                AutoSize  = true,
                ForeColor = TextMuted,
            };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private TextBox MakeTextBox(Control parent, int x, int y, int w, bool isPassword = false)
        {
            var pnl = new Panel
            {
                Location  = new Point(x, y),
                Size      = new Size(w, 32),
                BackColor = Color.Transparent
            };
            pnl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, w - 1, 31), 6);
                using var fill = new SolidBrush(Color.FromArgb(248, 250, 252));
                e.Graphics.FillPath(fill, path);
                using var pen = new Pen(BorderClr, 1f);
                e.Graphics.DrawPath(pen, path);
            };

            var txt = new TextBox
            {
                Location         = new Point(8, 6),
                Size             = new Size(w - 16, 20),
                BorderStyle      = BorderStyle.None,
                BackColor        = Color.FromArgb(248, 250, 252),
                ForeColor        = TextDark,
                Font             = new Font("Microsoft YaHei UI", 10f),
                UseSystemPasswordChar = isPassword,
            };
            
            pnl.Controls.Add(txt);
            parent.Controls.Add(pnl);
            return txt;
        }

        private ComboBox MakeComboBox(Control parent, int x, int y, int w, string[]? items = null)
        {
            var pnl = new Panel
            {
                Location  = new Point(x, y),
                Size      = new Size(w, 32),
                BackColor = Color.Transparent
            };
            pnl.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, w - 1, 31), 6);
                using var fill = new SolidBrush(Color.FromArgb(248, 250, 252));
                e.Graphics.FillPath(fill, path);
                using var pen = new Pen(BorderClr, 1f);
                e.Graphics.DrawPath(pen, path);
            };

            var cbo = new ComboBox
            {
                Location      = new Point(4, 4),
                Size          = new Size(w - 8, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle     = FlatStyle.Flat,
                BackColor     = Color.FromArgb(248, 250, 252),
                ForeColor     = TextDark,
                Font          = new Font("Microsoft YaHei UI", 9.5f)
            };
            if (items != null)
            {
                cbo.Items.AddRange(items);
                cbo.SelectedIndex = 0;
            }
            pnl.Controls.Add(cbo);
            parent.Controls.Add(pnl);
            return cbo;
        }

        private CheckBox MakeCheckBox(Control parent, string text, int x, int y)
        {
            var chk = new CheckBox
            {
                Text      = text,
                Location  = new Point(x, y),
                AutoSize  = true,
                ForeColor = TextDark,
            };
            parent.Controls.Add(chk);
            return chk;
        }

        private Button MakeButton(Control parent, string text, int x, int y,
            int w, int h, Color bg, Color bgHover)
        {
            var btn = new Button
            {
                Text      = "",
                Location  = new Point(x, y),
                Size      = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            
            bool isHovered = false;
            btn.MouseEnter += (s, e) => { isHovered = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { isHovered = false; btn.Invalidate(); };

            btn.Paint += (s, e) => {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, w - 1, h - 1), 8);
                using var fill = new SolidBrush(isHovered ? bgHover : bg);
                g.FillPath(fill, path);
                
                using var font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
                TextRenderer.DrawText(g, text, font, new Rectangle(0, 0, w, h), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            
            parent.Controls.Add(btn);
            return btn;
        }

        private Button MakeCircularIconButton(Control parent, string text, int x, int y, int size, float fontSize)
        {
            var btn = new Button
            {
                Text      = "",
                Location  = new Point(x, y),
                Size      = new Size(size, size),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;

            Color bgHover = Color.FromArgb(241, 245, 249); // Slate 100
            bool isHovered = false;
            
            btn.MouseEnter += (s, e) => { isHovered = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { isHovered = false; btn.Invalidate(); };

            btn.Paint += (s, e) => {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                
                if (isHovered) {
                    var rect = new Rectangle(0, 0, size - 1, size - 1);
                    using var fill = new SolidBrush(bgHover);
                    g.FillEllipse(fill, rect);
                }
                
                using var font = new Font("Segoe UI", fontSize, FontStyle.Bold);
                // Adjust string rectangle slightly down to visually center
                g.DrawString(text, font, new SolidBrush(TextDark), 
                    new Rectangle(0, 2, size, size), 
                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            
            parent.Controls.Add(btn);
            return btn;
        }

        private Button MakeShadowIconButton(Control parent, string text, int x, int y, int size, float fontSize)
        {
            int shadowOffset = 3;
            int totalSize = size + shadowOffset * 2 + 1;
            var btn = new Button
            {
                Text      = "",
                Location  = new Point(x - shadowOffset, y - shadowOffset),
                Size      = new Size(totalSize, totalSize),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor    = Cursors.Hand,
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;
            
            Color bg = Color.White;
            Color bgHover = Color.FromArgb(248, 250, 252);
            bool isHovered = false;
            
            btn.MouseEnter += (s, e) => { isHovered = true; btn.Invalidate(); };
            btn.MouseLeave += (s, e) => { isHovered = false; btn.Invalidate(); };

            btn.Paint += (s, e) => {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                
                // Draw circular shadow
                using var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)); // Light shadow
                g.FillEllipse(shadowBrush, new Rectangle(shadowOffset, shadowOffset + 1, size, size));
                
                // Draw main circular button
                int yOffset = isHovered ? 1 : 0;
                var buttonRect = new Rectangle(shadowOffset, shadowOffset - yOffset, size - 1, size - 1);
                
                using var fill = new SolidBrush(isHovered ? bgHover : bg);
                g.FillEllipse(fill, buttonRect);
                
                using var pen = new Pen(BorderClr, 1f);
                g.DrawEllipse(pen, buttonRect);
                
                using var font = new Font("Segoe UI", fontSize, FontStyle.Bold);
                var textRect = new Rectangle(shadowOffset, shadowOffset - yOffset , size, size);
                g.DrawString(text, font, new SolidBrush(TextDark), 
                    textRect, 
                    new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            };
            
            parent.Controls.Add(btn);
            return btn;
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

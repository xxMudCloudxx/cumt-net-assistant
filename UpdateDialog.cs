using System.Drawing.Drawing2D;
using AutoUpdaterDotNET;

namespace CampusNetAssistant
{
    public class UpdateDialog : Form
    {
        // 复用主题色
        private static readonly Color HeaderStart = Color.FromArgb(56, 189, 248);
        private static readonly Color HeaderEnd   = Color.FromArgb(99, 102, 241);
        private static readonly Color BgColor     = Color.FromArgb(248, 250, 252);
        private static readonly Color TextDark    = Color.FromArgb(15, 23, 42);
        private static readonly Color TextMuted   = Color.FromArgb(100, 116, 139);
        private static readonly Color BorderClr   = Color.FromArgb(226, 232, 240);
        private static readonly Color AccentGreen = Color.FromArgb(16, 185, 129);
        private static readonly Color AccentGreenDark = Color.FromArgb(5, 150, 105);

        private readonly UpdateInfoEventArgs _args;
        private bool _isHoveredUpdate = false;
        private bool _isHoveredSkip = false;

        public UpdateDialog(UpdateInfoEventArgs args)
        {
            _args = args;
            BuildUI();
        }

        private void BuildUI()
        {
            // ── 窗体属性 ──
            Text = "发现新版本";
            Size = new Size(420, 360); // 增加一点高度给系统标题栏
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = BgColor;
            DoubleBuffered = true;

            // ── 渐变头部 ──
            var header = new Panel { Dock = DockStyle.Top, Height = 90 };
            header.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var brush = new LinearGradientBrush(
                    header.ClientRectangle, HeaderStart, HeaderEnd, 45f);
                e.Graphics.FillRectangle(brush, header.ClientRectangle);

                // 图标和标题
                using var titleFont = new Font("Microsoft YaHei UI", 16f, FontStyle.Bold);
                TextRenderer.DrawText(e.Graphics, "🚀 发现新版本", titleFont,
                    new Point(24, 18), Color.White);

                // 版本号
                var currentVer = _args.InstalledVersion?.ToString() ?? "未知";
                var newVer = _args.CurrentVersion ?? "未知";
                using var subFont = new Font("Microsoft YaHei UI", 10f);
                TextRenderer.DrawText(e.Graphics, $"v{currentVer}  →  v{newVer}", subFont,
                    new Point(28, 54), Color.FromArgb(220, 255, 255, 255));
            };
            Controls.Add(header);

            // ── 内容区 ──
            var body = new Panel
            {
                Location = new Point(0, 90),
                Size = new Size(420, 250),
                BackColor = BgColor
            };
            Controls.Add(body);

            // 描述文字
            var lblDesc = new Label
            {
                Text = "有新版本可用，建议更新以获得最新功能和修复。",
                Location = new Point(28, 20),
                Size = new Size(364, 24),
                Font = new Font("Microsoft YaHei UI", 10f),
                ForeColor = TextDark,
                BackColor = Color.Transparent
            };
            body.Controls.Add(lblDesc);

            // Changelog 链接
            if (!string.IsNullOrEmpty(_args.ChangelogURL))
            {
                var lnkChangelog = new LinkLabel
                {
                    Text = "📋 查看更新日志",
                    Location = new Point(28, 52),
                    Size = new Size(200, 22),
                    Font = new Font("Microsoft YaHei UI", 9.5f),
                    LinkColor = HeaderEnd,
                    ActiveLinkColor = HeaderStart,
                    BackColor = Color.Transparent
                };
                lnkChangelog.LinkClicked += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = _args.ChangelogURL,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                };
                body.Controls.Add(lnkChangelog);
            }

            // ── 分隔线 ──
            var separator = new Panel
            {
                Location = new Point(28, 86),
                Size = new Size(364, 1),
                BackColor = BorderClr
            };
            body.Controls.Add(separator);

            // ── 下载大小提示 ──
            var lblNote = new Label
            {
                Text = "点击「立即更新」将下载并安装最新版本",
                Location = new Point(28, 100),
                Size = new Size(364, 20),
                Font = new Font("Microsoft YaHei UI", 9f),
                ForeColor = TextMuted,
                BackColor = Color.Transparent
            };
            body.Controls.Add(lblNote);

            // ── 按钮区 ──
            int btnY = 140;

            // 立即更新按钮
            var btnUpdate = new Button
            {
                Text = "",
                Location = new Point(28, btnY),
                Size = new Size(220, 46),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
            btnUpdate.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btnUpdate.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnUpdate.TabStop = false;
            btnUpdate.MouseEnter += (s, e) => { _isHoveredUpdate = true; btnUpdate.Invalidate(); };
            btnUpdate.MouseLeave += (s, e) => { _isHoveredUpdate = false; btnUpdate.Invalidate(); };
            btnUpdate.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, 219, 45), 10);
                using var fill = new SolidBrush(_isHoveredUpdate ? AccentGreenDark : AccentGreen);
                g.FillPath(fill, path);
                using var font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
                TextRenderer.DrawText(g, "✨ 立即更新", font,
                    new Rectangle(0, 0, 220, 46), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btnUpdate.Click += (s, e) =>
            {
                DialogResult = DialogResult.OK;
                Close();
            };
            body.Controls.Add(btnUpdate);

            // 稍后提醒按钮
            var btnLater = new Button
            {
                Text = "",
                Location = new Point(264, btnY),
                Size = new Size(128, 46),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btnLater.FlatAppearance.BorderSize = 0;
            btnLater.FlatAppearance.BorderColor = Color.FromArgb(0, 255, 255, 255);
            btnLater.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btnLater.FlatAppearance.MouseOverBackColor = Color.Transparent;
            btnLater.TabStop = false;
            btnLater.MouseEnter += (s, e) => { _isHoveredSkip = true; btnLater.Invalidate(); };
            btnLater.MouseLeave += (s, e) => { _isHoveredSkip = false; btnLater.Invalidate(); };
            btnLater.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(new Rectangle(0, 0, 127, 45), 10);
                using var fill = new SolidBrush(_isHoveredSkip
                    ? Color.FromArgb(226, 232, 240) : Color.FromArgb(241, 245, 249));
                g.FillPath(fill, path);
                using var font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
                TextRenderer.DrawText(g, "忽略此版本", font,
                    new Rectangle(0, 0, 128, 46), TextDark,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            btnLater.Click += (s, e) =>
            {
                DialogResult = DialogResult.Ignore;
                Close();
            };
            body.Controls.Add(btnLater);

        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

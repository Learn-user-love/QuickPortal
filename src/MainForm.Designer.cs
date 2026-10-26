// -------------------------------------------------------------------------
// QuickPortal - Quick Portal authentication helper
// Copyright (C) 2026 QuickPortal contributors
// Licensed under the GNU General Public License v3.0 or later (see COPYING).
// -------------------------------------------------------------------------
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace QuickPortal
{
    public partial class MainForm
    {
        private void InitializeComponent()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 98));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = Color.FromArgb(15, 23, 42);
            root.Controls.Add(header, 0, 0);

            PictureBox logo = new PictureBox();
            logo.Location = new Point(22, 19);
            logo.Size = new Size(60, 60);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Color.Transparent;
            _logoImage = LoadLogoImage();
            if (_logoImage != null) logo.Image = _logoImage;
            else
            {
                try { if (Icon != null) logo.Image = Icon.ToBitmap(); } catch { }
            }
            header.Controls.Add(logo);

            Label title = new Label();
            title.Text = "QuickPortal";
            title.AutoSize = true;
            title.Location = new Point(98, 7);
            title.Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold);
            title.ForeColor = Color.White;
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "快速 Web / Portal 认证  ·  Windows 11";
            subtitle.AutoSize = true;
            subtitle.Location = new Point(100, 58);
            subtitle.Font = new Font("Microsoft YaHei UI", 10.5F);
            subtitle.ForeColor = Color.FromArgb(148, 163, 184);
            header.Controls.Add(subtitle);

            _headerStatus = new Label();
            _headerStatus.Text = "● 正在检测";
            _headerStatus.Dock = DockStyle.Right;
            _headerStatus.Width = 320;
            _headerStatus.TextAlign = ContentAlignment.MiddleRight;
            _headerStatus.Padding = new Padding(0, 0, 24, 0);
            _headerStatus.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold);
            _headerStatus.ForeColor = Color.FromArgb(148, 163, 184);
            header.Controls.Add(_headerStatus);

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = new Font("Microsoft YaHei UI", 11.5F);
            _tabs.Padding = new Point(18, 6);
            root.Controls.Add(_tabs, 0, 1);

            _homePage = MakeTab("概览");
            _settingsPage = MakeTab("认证设置");
            _automationPage = MakeTab("自动化");
            _logsPage = MakeTab("日志");
            _tabs.TabPages.Add(_homePage);
            _tabs.TabPages.Add(_settingsPage);
            _tabs.TabPages.Add(_automationPage);
            _tabs.TabPages.Add(_logsPage);

            BuildHomePage();
            BuildSettingsPage();
            BuildAutomationPage();
            BuildLogsPage();

            StatusStrip strip = new StatusStrip();
            strip.Dock = DockStyle.Fill;
            strip.SizingGrip = false;
            strip.BackColor = Color.White;
            strip.ForeColor = Muted;
            strip.Font = new Font("Microsoft YaHei UI", 10F);
            _stripLabel = new ToolStripStatusLabel("就绪");
            _stripLabel.Spring = true;
            _stripLabel.TextAlign = ContentAlignment.MiddleLeft;
            strip.Items.Add(_stripLabel);
            ToolStripStatusLabel ver = new ToolStripStatusLabel("v1.0.5");
            ver.ForeColor = Muted;
            strip.Items.Add(ver);
            root.Controls.Add(strip, 0, 2);
        }

        private TabPage MakeTab(string text)
        {
            TabPage page = new TabPage(text);
            page.BackColor = Bg;
            page.Padding = new Padding(12);
            return page;
        }

        private void BuildHomePage()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(4);
            _homePage.Controls.Add(layout);

            GroupBox statusGroup = new GroupBox();
            statusGroup.Text = "连接状态";
            statusGroup.Dock = DockStyle.Fill;
            statusGroup.BackColor = Card;
            statusGroup.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            statusGroup.ForeColor = TextColor;
            statusGroup.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(statusGroup, 0, 0);

            _bigStatus = new Label();
            _bigStatus.Text = "正在检测...";
            _bigStatus.AutoSize = true;
            _bigStatus.Location = new Point(20, 26);
            _bigStatus.Font = new Font("Microsoft YaHei UI", 26F, FontStyle.Bold);
            _bigStatus.ForeColor = TextColor;
            statusGroup.Controls.Add(_bigStatus);

            _statusDetail = new Label();
            _statusDetail.Text = "正在读取网络状态，请稍候。";
            _statusDetail.AutoSize = false;
            _statusDetail.Location = new Point(23, 88);
            _statusDetail.Size = new Size(780, 26);
            _statusDetail.ForeColor = Muted;
            _statusDetail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            statusGroup.Controls.Add(_statusDetail);

            _networkInfo = new Label();
            _networkInfo.Text = "网络：—";
            _networkInfo.AutoSize = false;
            _networkInfo.Location = new Point(23, 118);
            _networkInfo.Size = new Size(780, 26);
            _networkInfo.ForeColor = Muted;
            _networkInfo.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            statusGroup.Controls.Add(_networkInfo);

            _lastAuthLabel = new Label();
            _lastAuthLabel.Text = "上次认证：—";
            _lastAuthLabel.AutoSize = false;
            _lastAuthLabel.Location = new Point(23, 146);
            _lastAuthLabel.Size = new Size(780, 26);
            _lastAuthLabel.ForeColor = Muted;
            _lastAuthLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            statusGroup.Controls.Add(_lastAuthLabel);

            Button authButton = MakeButton("立即快速认证", Primary, Color.White, 190, 46);
            authButton.Location = new Point(22, 172);
            authButton.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            authButton.Click += delegate { AuthNow(); };
            statusGroup.Controls.Add(authButton);

            Button openButton = MakeButton("打开认证页", Card, TextColor, 145, 46);
            openButton.Location = new Point(222, 172);
            openButton.Click += delegate { OpenPortalOnly(); };
            statusGroup.Controls.Add(openButton);

            Button checkButton = MakeButton("重新检测", Card, TextColor, 130, 46);
            checkButton.Location = new Point(377, 172);
            checkButton.Click += async delegate { await CheckConnectivityAsync(true, false); };
            statusGroup.Controls.Add(checkButton);

            statusGroup.Resize += delegate
            {
                int width = Math.Max(200, statusGroup.ClientSize.Width - 46);
                _statusDetail.Width = width;
                _networkInfo.Width = width;
                _lastAuthLabel.Width = width;
            };

            GroupBox quickGroup = new GroupBox();
            quickGroup.Text = "快捷开关（修改后立即保存）";
            quickGroup.Dock = DockStyle.Fill;
            quickGroup.BackColor = Card;
            quickGroup.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            quickGroup.ForeColor = TextColor;
            quickGroup.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(quickGroup, 0, 1);

            FlowLayoutPanel quickFlow = new FlowLayoutPanel();
            quickFlow.Dock = DockStyle.Fill;
            quickFlow.Padding = new Padding(14, 12, 14, 8);
            quickFlow.WrapContents = true;
            quickGroup.Controls.Add(quickFlow);

            _quickAutoStart = MakeCheckBox("开机自动启动");
            _quickAutoStart.Margin = new Padding(0, 2, 30, 6);
            _quickAutoStart.CheckedChanged += delegate { OnAutostartCheckChanged(_quickAutoStart, _startWithWindows); };
            quickFlow.Controls.Add(_quickAutoStart);

            _quickAutoMonitor = MakeCheckBox("断网自动重连");
            _quickAutoMonitor.Margin = new Padding(0, 2, 30, 6);
            _quickAutoMonitor.CheckedChanged += delegate { OnAutoMonitorCheckChanged(_quickAutoMonitor, _autoMonitor); };
            quickFlow.Controls.Add(_quickAutoMonitor);

            _quickStartupAuth = MakeCheckBox("启动后自动认证");
            _quickStartupAuth.Margin = new Padding(0, 2, 30, 6);
            _quickStartupAuth.CheckedChanged += delegate { OnAutoStartupCheckChanged(_quickStartupAuth, _autoStartup); };
            quickFlow.Controls.Add(_quickStartupAuth);

            GroupBox recentGroup = new GroupBox();
            recentGroup.Text = "最近活动";
            recentGroup.Dock = DockStyle.Fill;
            recentGroup.BackColor = Card;
            recentGroup.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            recentGroup.ForeColor = TextColor;
            layout.Controls.Add(recentGroup, 0, 2);

            _recentBox = new TextBox();
            _recentBox.Multiline = true;
            _recentBox.ReadOnly = true;
            _recentBox.ScrollBars = ScrollBars.Vertical;
            _recentBox.Dock = DockStyle.Fill;
            _recentBox.BorderStyle = BorderStyle.None;
            _recentBox.BackColor = Card;
            _recentBox.ForeColor = TextColor;
            _recentBox.Font = new Font("Microsoft YaHei UI", 10.5F);
            _recentBox.Margin = new Padding(14);
            _recentBox.WordWrap = true;
            recentGroup.Controls.Add(_recentBox);
            _recentBox.BringToFront();
        }

        private void BuildSettingsPage()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 375));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 185));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(4);
            _settingsPage.Controls.Add(layout);

            GroupBox group = new GroupBox();
            group.Text = "Portal 登录信息";
            group.Dock = DockStyle.Fill;
            group.BackColor = Card;
            group.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            group.ForeColor = TextColor;
            group.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(group, 0, 0);

            TableLayoutPanel form = new TableLayoutPanel();
            form.Dock = DockStyle.Fill;
            form.Padding = new Padding(14, 12, 14, 8);
            form.ColumnCount = 2;
            form.RowCount = 6;
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            group.Controls.Add(form);

            form.Controls.Add(MakeFieldLabel("Portal 地址"), 0, 0);
            TableLayoutPanel urlPanel = new TableLayoutPanel();
            urlPanel.Dock = DockStyle.Fill;
            urlPanel.ColumnCount = 2;
            urlPanel.RowCount = 1;
            urlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            urlPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            urlPanel.Margin = new Padding(0);
            form.Controls.Add(urlPanel, 1, 0);

            _urlBox = MakeTextBox();
            _urlBox.Dock = DockStyle.Fill;
            _urlBox.Margin = new Padding(0, 4, 8, 4);
            urlPanel.Controls.Add(_urlBox, 0, 0);

            _detectButton = MakeButton("自动探测", Card, Primary, 100, 30);
            _detectButton.Dock = DockStyle.Fill;
            _detectButton.Margin = new Padding(0, 4, 0, 4);
            _detectButton.Click += async delegate { await DetectPortalAsync(); };
            urlPanel.Controls.Add(_detectButton, 1, 0);

            form.Controls.Add(MakeFieldLabel("账号 / 用户名"), 0, 1);
            _userBox = MakeTextBox();
            _userBox.Dock = DockStyle.Fill;
            _userBox.Margin = new Padding(0, 4, 0, 4);
            form.Controls.Add(_userBox, 1, 1);

            form.Controls.Add(MakeFieldLabel("密码"), 0, 2);
            TableLayoutPanel pwdPanel = new TableLayoutPanel();
            pwdPanel.Dock = DockStyle.Fill;
            pwdPanel.ColumnCount = 2;
            pwdPanel.RowCount = 1;
            pwdPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pwdPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            pwdPanel.Margin = new Padding(0);
            form.Controls.Add(pwdPanel, 1, 2);

            _pwdBox = MakeTextBox();
            _pwdBox.Dock = DockStyle.Fill;
            _pwdBox.UseSystemPasswordChar = true;
            _pwdBox.Margin = new Padding(0, 4, 8, 4);
            pwdPanel.Controls.Add(_pwdBox, 0, 0);

            _showPwd = MakeCheckBox("显示密码");
            _showPwd.Dock = DockStyle.Fill;
            _showPwd.Margin = new Padding(0, 4, 0, 4);
            _showPwd.CheckedChanged += delegate { _pwdBox.UseSystemPasswordChar = !_showPwd.Checked; };
            pwdPanel.Controls.Add(_showPwd, 1, 0);

            FlowLayoutPanel options = new FlowLayoutPanel();
            options.Dock = DockStyle.Fill;
            options.WrapContents = false;
            options.Margin = new Padding(0, 0, 0, 0);
            form.Controls.Add(options, 1, 3);

            _autoFill = MakeCheckBox("自动填充账号密码");
            _autoFill.Margin = new Padding(0, 4, 30, 0);
            _autoFill.CheckedChanged += delegate
            {
                if (_loading) return;
                _config.AutoFill = _autoFill.Checked;
                SaveQuiet();
            };
            options.Controls.Add(_autoFill);

            _autoSubmit = MakeCheckBox("自动点击登录 / 认证");
            _autoSubmit.Margin = new Padding(0, 4, 20, 0);
            _autoSubmit.CheckedChanged += delegate
            {
                if (_loading) return;
                _config.AutoSubmit = _autoSubmit.Checked;
                SaveQuiet();
            };
            options.Controls.Add(_autoSubmit);

            Label hint = new Label();
            hint.Text = "密码使用 Windows DPAPI 加密保存在本机；遇到验证码会自动停下并显示窗口。";
            hint.Dock = DockStyle.Fill;
            hint.ForeColor = Muted;
            hint.Font = new Font("Microsoft YaHei UI", 10F);
            hint.Margin = new Padding(0, 2, 0, 2);
            form.Controls.Add(hint, 1, 4);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0);
            form.Controls.Add(buttons, 1, 5);

            Button saveButton = MakeButton("保存设置", Primary, Color.White, 118, 36);
            saveButton.Margin = new Padding(0, 4, 10, 0);
            saveButton.Click += delegate
            {
                if (SaveSettingsFromUi(true))
                {
                    _stripLabel.Text = "设置已保存。";
                }
            };
            buttons.Controls.Add(saveButton);

            Button testButton = MakeButton("立即测试认证", Card, Primary, 132, 36);
            testButton.Margin = new Padding(0, 4, 10, 0);
            testButton.Click += delegate { AuthNow(); };
            buttons.Controls.Add(testButton);

            GroupBox tips = new GroupBox();
            tips.Text = "使用提示";
            tips.Dock = DockStyle.Fill;
            tips.BackColor = Card;
            tips.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            tips.ForeColor = TextColor;
            layout.Controls.Add(tips, 0, 1);

            Label tipsText = new Label();
            tipsText.Dock = DockStyle.Fill;
            tipsText.Padding = new Padding(16, 12, 16, 10);
            tipsText.Font = new Font("Microsoft YaHei UI", 10F);
            tipsText.ForeColor = Color.FromArgb(71, 85, 105);
            tipsText.Text =
                "1. 填写 Portal 登录页地址（通常是由网络自动跳转到的那个网页）。点击“自动探测”可尝试从当前网络获取。\r\n" +
                "2. “自动填充 + 自动点击”适用于绝大多数账号密码型 Web 认证页；如果页面有验证码、短信或特殊控件，程序会停下来让你手动完成。\r\n" +
                "3. 建议把程序放在固定目录后再开启开机自启，移动程序后需要重新勾选一次。";
            tips.Controls.Add(tipsText);
        }

        private void BuildAutomationPage()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 3;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 240));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 276));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Padding = new Padding(4);
            _automationPage.Controls.Add(layout);

            GroupBox startup = new GroupBox();
            startup.Text = "开机与启动";
            startup.Dock = DockStyle.Fill;
            startup.BackColor = Card;
            startup.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            startup.ForeColor = TextColor;
            startup.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(startup, 0, 0);

            FlowLayoutPanel startupFlow = new FlowLayoutPanel();
            startupFlow.Dock = DockStyle.Fill;
            startupFlow.FlowDirection = FlowDirection.TopDown;
            startupFlow.WrapContents = false;
            startupFlow.Padding = new Padding(16, 12, 16, 8);
            startup.Controls.Add(startupFlow);

            _startWithWindows = MakeCheckBox("开机自动启动 QuickPortal（当前用户，无需管理员权限）");
            _startWithWindows.Margin = new Padding(0, 3, 0, 4);
            _startWithWindows.CheckedChanged += delegate { OnAutostartCheckChanged(_startWithWindows, _quickAutoStart); };
            startupFlow.Controls.Add(_startWithWindows);

            _autoStartup = MakeCheckBox("Windows 启动后自动尝试认证");
            _autoStartup.Margin = new Padding(0, 3, 0, 4);
            _autoStartup.CheckedChanged += delegate { OnAutoStartupCheckChanged(_autoStartup, _quickStartupAuth); };
            startupFlow.Controls.Add(_autoStartup);

            _silentAuto = MakeCheckBox("静默认证：自动认证时不弹出认证窗口（遇到验证码/失败时会自动显示）");
            _silentAuto.Margin = new Padding(0, 3, 0, 4);
            _silentAuto.CheckedChanged += delegate
            {
                if (_loading) return;
                _config.SilentAuto = _silentAuto.Checked;
                SaveQuiet();
            };
            startupFlow.Controls.Add(_silentAuto);

            _regInfo = new Label();
            _regInfo.AutoSize = false;
            _regInfo.Size = new Size(760, 24);
            _regInfo.ForeColor = Muted;
            _regInfo.Font = new Font("Microsoft YaHei UI", 10F);
            _regInfo.Margin = new Padding(0, 6, 0, 0);
            startupFlow.Controls.Add(_regInfo);

            GroupBox monitor = new GroupBox();
            monitor.Text = "断网自动重连";
            monitor.Dock = DockStyle.Fill;
            monitor.BackColor = Card;
            monitor.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            monitor.ForeColor = TextColor;
            monitor.Margin = new Padding(0, 0, 0, 10);
            layout.Controls.Add(monitor, 0, 1);

            FlowLayoutPanel monitorFlow = new FlowLayoutPanel();
            monitorFlow.Dock = DockStyle.Fill;
            monitorFlow.FlowDirection = FlowDirection.TopDown;
            monitorFlow.WrapContents = false;
            monitorFlow.Padding = new Padding(16, 12, 16, 8);
            monitor.Controls.Add(monitorFlow);

            _autoMonitor = MakeCheckBox("定时检测网络，断网时自动尝试 Portal 认证");
            _autoMonitor.Margin = new Padding(0, 3, 0, 4);
            _autoMonitor.CheckedChanged += delegate { OnAutoMonitorCheckChanged(_autoMonitor, _quickAutoMonitor); };
            monitorFlow.Controls.Add(_autoMonitor);

            FlowLayoutPanel intervalPanel = new FlowLayoutPanel();
            intervalPanel.AutoSize = true;
            intervalPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            intervalPanel.WrapContents = false;
            intervalPanel.FlowDirection = FlowDirection.LeftToRight;
            intervalPanel.Margin = new Padding(0, 2, 0, 4);
            monitorFlow.Controls.Add(intervalPanel);

            Label intervalLabel = new Label();
            intervalLabel.Text = "检测间隔：";
            intervalLabel.AutoSize = true;
            intervalLabel.Margin = new Padding(0, 7, 6, 0);
            intervalLabel.ForeColor = TextColor;
            intervalLabel.Font = new Font("Microsoft YaHei UI", 10.5F);
            intervalPanel.Controls.Add(intervalLabel);

            _interval = new NumericUpDown();
            _interval.Width = 100;
            _interval.Font = new Font("Microsoft YaHei UI", 10.5F);
            _interval.TextAlign = HorizontalAlignment.Left;
            _interval.Margin = new Padding(0, 3, 6, 0);
            _interval.Minimum = 15;
            _interval.Maximum = 300;
            _interval.Increment = 15;
            _interval.ValueChanged += delegate
            {
                if (_loading) return;
                _config.CheckIntervalSeconds = (int)_interval.Value;
                SaveQuiet();
                RestartMonitorTimer();
            };
            intervalPanel.Controls.Add(_interval);

            Label seconds = new Label();
            seconds.Text = "秒";
            seconds.AutoSize = true;
            seconds.Margin = new Padding(0, 7, 0, 0);
            seconds.ForeColor = TextColor;
            seconds.Font = new Font("Microsoft YaHei UI", 10.5F);
            intervalPanel.Controls.Add(seconds);

            _notify = MakeCheckBox("认证成功 / 失败时显示系统通知");
            _notify.Margin = new Padding(0, 3, 0, 4);
            _notify.CheckedChanged += delegate
            {
                if (_loading) return;
                _config.Notify = _notify.Checked;
                SaveQuiet();
            };
            monitorFlow.Controls.Add(_notify);

            _closeTray = MakeCheckBox("关闭主窗口时最小化到系统托盘");
            _closeTray.Margin = new Padding(0, 3, 0, 4);
            _closeTray.CheckedChanged += delegate
            {
                if (_loading) return;
                _config.CloseToTray = _closeTray.Checked;
                SaveQuiet();
            };
            monitorFlow.Controls.Add(_closeTray);

            GroupBox notes = new GroupBox();
            notes.Text = "说明";
            notes.Dock = DockStyle.Fill;
            notes.BackColor = Card;
            notes.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
            notes.ForeColor = TextColor;
            layout.Controls.Add(notes, 0, 2);

            Label notesText = new Label();
            notesText.Dock = DockStyle.Fill;
            notesText.Padding = new Padding(16, 12, 16, 10);
            notesText.Font = new Font("Microsoft YaHei UI", 10F);
            notesText.ForeColor = Color.FromArgb(71, 85, 105);
            notesText.Text =
                "开机自启写入当前用户注册表 HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run，不会触发 UAC。\r\n" +
                "断网检测使用系统网络状态与 HTTP 探测，认证过程在内置 Edge WebView2 中完成，不会把密码发送给本程序以外的服务器。\r\n" +
                "如果关闭“断网自动重连”，仍可随时在“概览”页手动点击“立即快速认证”。";
            notes.Controls.Add(notesText);
        }

        private void BuildLogsPage()
        {
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _logsPage.Controls.Add(layout);

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.BackColor = Bg;
            layout.Controls.Add(toolbar, 0, 0);

            Button openFolder = MakeButton("打开日志目录", Card, TextColor, 120, 32);
            openFolder.Location = new Point(2, 4);
            openFolder.Click += delegate { NetworkHelper.OpenFolder(ConfigStore.LogDir); };
            toolbar.Controls.Add(openFolder);

            Button clear = MakeButton("清空显示", Card, TextColor, 96, 32);
            clear.Location = new Point(130, 4);
            clear.Click += delegate { _logRich.Clear(); _recentBox.Clear(); };
            toolbar.Controls.Add(clear);

            Button refresh = MakeButton("重新载入", Card, TextColor, 96, 32);
            refresh.Location = new Point(234, 4);
            refresh.Click += delegate { LoadRecentLog(true); };
            toolbar.Controls.Add(refresh);

            Label path = new Label();
            path.Text = "日志文件：" + Logger.CurrentLogFile;
            path.AutoSize = false;
            path.Dock = DockStyle.Right;
            path.Width = 500;
            path.TextAlign = ContentAlignment.MiddleRight;
            path.ForeColor = Muted;
            path.Font = new Font("Microsoft YaHei UI", 9.5F);
            toolbar.Controls.Add(path);

            _logRich = new RichTextBox();
            _logRich.Dock = DockStyle.Fill;
            _logRich.ReadOnly = true;
            _logRich.BackColor = Color.FromArgb(15, 23, 42);
            _logRich.ForeColor = Color.FromArgb(226, 232, 240);
            _logRich.BorderStyle = BorderStyle.None;
            _logRich.Font = new Font("Consolas", 10F);
            _logRich.WordWrap = false;
            _logRich.ScrollBars = RichTextBoxScrollBars.Both;
            _logRich.DetectUrls = false;
            layout.Controls.Add(_logRich, 0, 1);
        }

        private Image LoadLogoImage()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream("QuickPortal.quickportal-256.png"))
                {
                    if (stream == null) return null;
                    using (Image image = Image.FromStream(stream))
                    {
                        return new Bitmap(image);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private Label MakeFieldLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = TextColor;
            label.Font = new Font("Microsoft YaHei UI", 10.5F);
            label.Margin = new Padding(0, 4, 8, 4);
            return label;
        }

        private TextBox MakeTextBox()
        {
            TextBox box = new TextBox();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = new Font("Microsoft YaHei UI", 10.5F);
            box.BackColor = Color.White;
            box.ForeColor = TextColor;
            return box;
        }

        private CheckBox MakeCheckBox(string text)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.AutoSize = true;
            box.ForeColor = TextColor;
            box.Font = new Font("Microsoft YaHei UI", 10.5F);
            box.BackColor = Color.Transparent;
            box.Cursor = Cursors.Hand;
            return box;
        }

        private Button MakeButton(string text, Color backColor, Color foreColor, int width, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = height;
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = backColor == Card ? 1 : 0;
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            button.Font = new Font("Microsoft YaHei UI", 10.5F);
            button.Cursor = Cursors.Hand;
            return button;
        }
    }
}

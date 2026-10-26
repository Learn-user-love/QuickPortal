// -------------------------------------------------------------------------
// QuickPortal - Quick Portal authentication helper
// Copyright (C) 2026 QuickPortal contributors
// Licensed under the GNU General Public License v3.0 or later (see COPYING).
// -------------------------------------------------------------------------
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace QuickPortal
{
    public partial class MainForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(244, 246, 250);
        private static readonly Color Card = Color.White;
        private static readonly Color Primary = Color.FromArgb(37, 99, 235);
        private static readonly Color TextColor = Color.FromArgb(15, 23, 42);
        private static readonly Color Muted = Color.FromArgb(100, 116, 139);
        private static readonly Color Green = Color.FromArgb(22, 163, 74);
        private static readonly Color Orange = Color.FromArgb(194, 65, 12);
        private static readonly Color Red = Color.FromArgb(185, 28, 28);

        private readonly AppConfig _config;
        private readonly bool _startedByAutostart;

        private TabControl _tabs;
        private TabPage _homePage;
        private TabPage _settingsPage;
        private TabPage _automationPage;
        private TabPage _logsPage;

        private Label _headerStatus;
        private Label _bigStatus;
        private Label _statusDetail;
        private Label _networkInfo;
        private Label _lastAuthLabel;
        private Label _regInfo;
        private ToolStripStatusLabel _stripLabel;

        private TextBox _urlBox;
        private TextBox _userBox;
        private TextBox _pwdBox;
        private TextBox _recentBox;
        private RichTextBox _logRich;
        private CheckBox _showPwd;
        private CheckBox _autoFill;
        private CheckBox _autoSubmit;
        private CheckBox _quickAutoStart;
        private CheckBox _quickAutoMonitor;
        private CheckBox _quickStartupAuth;
        private CheckBox _startWithWindows;
        private CheckBox _autoStartup;
        private CheckBox _silentAuto;
        private CheckBox _autoMonitor;
        private CheckBox _notify;
        private CheckBox _closeTray;
        private NumericUpDown _interval;
        private Button _detectButton;

        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayAutoStart;
        private Image _logoImage;
        private System.Windows.Forms.Timer _monitorTimer;
        private System.Windows.Forms.Timer _networkDebounce;
        private System.Windows.Forms.Timer _trayBalloonTimer;

        private LoginForm _loginForm;
        private bool _authInProgress;
        private bool _authSucceeded;
        private bool _checking;
        private bool _loading = true;
        private bool _exiting;
        private bool _trayHintShown;
        private DateTime _lastAutoAttempt = DateTime.MinValue;
        private DateTime _lastAuthSuccessUtc = DateTime.MinValue;
        private int _autoFailures;

        public MainForm(AppConfig config, bool startedByAutostart)
        {
            _config = config;
            _startedByAutostart = startedByAutostart;

            Text = Native.MainWindowTitle;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1100, 820);
            MinimumSize = new Size(980, 720);
            BackColor = Bg;
            Font = new Font("Microsoft YaHei UI", 11F);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            if (_startedByAutostart)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }

            InitializeComponent();
            BuildTray();
            BuildTimers();
            // Scale after the control tree exists, otherwise WinForms scales only
            // the empty form and later-added controls stay at 96-DPI dimensions.
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            Load += async delegate { await OnLoadedAsync(); };
            Shown += delegate
            {
                if (_startedByAutostart && Visible)
                {
                    BeginInvoke(new Action(delegate
                    {
                        Hide();
                        ShowInTaskbar = false;
                    }));
                }
            };
            FormClosing += OnFormClosing;
            FormClosed += OnFormClosed;
            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && _config.CloseToTray && !_startedByAutostart)
                {
                    Hide();
                    ShowTrayHint();
                }
            };
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.ShowMainMessage)
            {
                ShowMainWindow();
                return;
            }
            base.WndProc(ref m);
        }

        private void BuildTray()
        {
            _trayMenu = new ContextMenuStrip();
            ToolStripMenuItem show = new ToolStripMenuItem("显示主界面");
            show.Font = new Font(show.Font, FontStyle.Bold);
            show.Click += delegate { ShowMainWindow(); };
            _trayMenu.Items.Add(show);

            ToolStripMenuItem auth = new ToolStripMenuItem("立即快速认证");
            auth.Click += delegate { AuthNow(); };
            _trayMenu.Items.Add(auth);

            ToolStripMenuItem open = new ToolStripMenuItem("打开认证页");
            open.Click += delegate { OpenPortalOnly(); };
            _trayMenu.Items.Add(open);

            _trayMenu.Items.Add(new ToolStripSeparator());

            _trayAutoStart = new ToolStripMenuItem("开机自启");
            _trayAutoStart.CheckOnClick = true;
            _trayAutoStart.Click += delegate { ApplyAutostartFromTray(_trayAutoStart.Checked); };
            _trayMenu.Items.Add(_trayAutoStart);

            _trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("退出 QuickPortal");
            exit.Click += delegate { ExitApplication(); };
            _trayMenu.Items.Add(exit);

            _tray = new NotifyIcon();
            _tray.Icon = Icon != null ? Icon : SystemIcons.Application;
            _tray.Text = "QuickPortal - 快速认证";
            _tray.ContextMenuStrip = _trayMenu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowMainWindow(); };
        }

        private void BuildTimers()
        {
            _monitorTimer = new System.Windows.Forms.Timer();
            _monitorTimer.Interval = 30000;
            _monitorTimer.Tick += async delegate
            {
                if (!_config.AutoMonitor || _checking || _authInProgress) return;
                await CheckConnectivityAsync(false, true);
            };

            _networkDebounce = new System.Windows.Forms.Timer();
            _networkDebounce.Interval = 4000;
            _networkDebounce.Tick += async delegate
            {
                _networkDebounce.Stop();
                if (!_config.AutoMonitor || _checking) return;
                await CheckConnectivityAsync(false, true);
            };

            _trayBalloonTimer = new System.Windows.Forms.Timer();
            _trayBalloonTimer.Interval = 800;
            _trayBalloonTimer.Tick += delegate
            {
                _trayBalloonTimer.Stop();
                if (_startedByAutostart) ShowBalloon("QuickPortal", "已在后台启动。双击托盘图标可打开主界面。", ToolTipIcon.Info);
            };

            NetworkChange.NetworkAddressChanged += OnNetworkChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        }

        private void OnNetworkChanged(object sender, EventArgs e)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(delegate
                {
                    _networkDebounce.Stop();
                    _networkDebounce.Start();
                }));
            }
            catch { }
        }

        private async Task OnLoadedAsync()
        {
            Logger.Logged += AppendLogLine;
            ApplyConfigToUi();
            _loading = false;
            UpdateAutostartInfo();
            LoadRecentLog(false);
            RestartMonitorTimer();

            Logger.Info("QuickPortal 已启动。");
            Logger.Info("数据目录：" + ConfigStore.DataDir);
            if (string.IsNullOrEmpty(_config.PortalUrl))
            {
                Logger.Info("尚未配置 Portal 地址。");
            }

            if (_startedByAutostart)
            {
                _trayBalloonTimer.Start();
            }

            ProbeResult result = await CheckConnectivityAsync(false, false);

            if (result != null && !result.Online)
            {
                if (_startedByAutostart && _config.AutoLoginOnStartup)
                {
                    if (!NetworkHelper.IsValidPortalUrl(_config.PortalUrl))
                    {
                        ShowBalloon("QuickPortal", "已经开机启动，但还没有配置 Portal 地址。请打开主界面完成设置。", ToolTipIcon.Warning);
                    }
                    else
                    {
                        TriggerAutoAuth();
                    }
                }
                else if (_config.AutoMonitor)
                {
                    MaybeAutoAuth(true);
                }
            }

            if (_startedByAutostart && _config.AutoLoginOnStartup == false && _config.AutoMonitor == false)
            {
                _stripLabel.Text = "已在后台运行，可从托盘打开。";
            }
        }

        private void ApplyConfigToUi()
        {
            _loading = true;
            _urlBox.Text = _config.PortalUrl;
            _userBox.Text = _config.Username;
            _pwdBox.Text = _config.GetPassword();
            _autoFill.Checked = _config.AutoFill;
            _autoSubmit.Checked = _config.AutoSubmit;
            _quickAutoMonitor.Checked = _config.AutoMonitor;
            _autoMonitor.Checked = _config.AutoMonitor;
            _quickStartupAuth.Checked = _config.AutoLoginOnStartup;
            _autoStartup.Checked = _config.AutoLoginOnStartup;
            _silentAuto.Checked = _config.SilentAuto;
            _notify.Checked = _config.Notify;
            _closeTray.Checked = _config.CloseToTray;
            _interval.Value = Math.Max(_interval.Minimum, Math.Min(_interval.Maximum, _config.CheckIntervalSeconds));

            bool autoStartEnabled = Autostart.IsEnabled();
            _startWithWindows.Checked = autoStartEnabled;
            _quickAutoStart.Checked = autoStartEnabled;
            _trayAutoStart.Checked = autoStartEnabled;
            _loading = false;

            UpdateLastAuthLabel();
        }

        private void UpdateAutostartInfo()
        {
            AutostartMethod method = Autostart.GetActiveMethod();
            if (method != AutostartMethod.None)
            {
                _regInfo.Text = "启动项状态：已启用  ·  " + Autostart.MethodText(method);
                _regInfo.ForeColor = Green;
            }
            else if (Autostart.HasAnyEntry())
            {
                _regInfo.Text = "启动项状态：存在旧路径的启动项，请重新勾选“开机自启”以更新。";
                _regInfo.ForeColor = Orange;
            }
            else
            {
                _regInfo.Text = "启动项状态：未启用（注册表被限制时会自动改用启动文件夹）";
                _regInfo.ForeColor = Muted;
            }
        }

        private void RestartMonitorTimer()
        {
            if (_monitorTimer == null) return;
            _monitorTimer.Stop();
            int seconds = Math.Max(10, _config.CheckIntervalSeconds);
            _monitorTimer.Interval = seconds * 1000;
            if (_config.AutoMonitor) _monitorTimer.Start();
        }

        private async Task<ProbeResult> CheckConnectivityAsync(bool manual, bool allowAuto)
        {
            if (_checking) return null;
            _checking = true;
            try
            {
                if (manual)
                {
                    SetConnectionState("正在检测...", "正在检查互联网连接，请稍候。", Muted, "● 正在检测");
                    _stripLabel.Text = "正在检测网络...";
                }
                else if (_bigStatus.Text == "正在检测...")
                {
                    SetConnectionState("正在检测...", "正在检查互联网连接，请稍候。", Muted, "● 正在检测");
                }

                ProbeResult result = await NetworkHelper.CheckAsync();
                string network = await Task.Run(new Func<string>(NetworkHelper.GetNetworkInfo));
                _networkInfo.Text = "网络：" + network;

                if (result.Online)
                {
                    SetConnectionState("已联网", "互联网连接正常。" + (result.LatencyMs > 0 ? " 探测耗时 " + result.LatencyMs + " ms。" : ""), Green, "● 已联网");
                    _stripLabel.Text = "网络正常。";
                    _autoFailures = 0;
                    if (manual) Logger.Success("网络检测：已联网。");
                }
                else
                {
                    if (InSuccessGrace())
                    {
                        SetConnectionState("已认证", "Portal 已登录成功；部分联网检测地址暂时不可达，已暂停自动重试，避免重复弹出认证窗口。",
                            Green, "● 已认证");
                        _stripLabel.Text = "已认证。";
                        return result;
                    }

                    if (string.IsNullOrEmpty(_config.PortalUrl) && !string.IsNullOrEmpty(result.DetectedPortalUrl))
                    {
                        _config.PortalUrl = result.DetectedPortalUrl;
                        _urlBox.Text = _config.PortalUrl;
                        ConfigStore.Save(_config);
                        Logger.Info("检测到 Portal 地址并自动填入：" + result.DetectedPortalUrl);
                    }

                    string detail = result.Detail;
                    if (string.IsNullOrEmpty(_config.PortalUrl))
                    {
                        detail += " 尚未配置 Portal 地址，请到“认证设置”填写。";
                    }
                    else
                    {
                        detail += " 可以点击“立即快速认证”。";
                    }

                    SetConnectionState(NetworkHelper.IsValidPortalUrl(_config.PortalUrl) ? "需要认证" : "未配置认证页",
                        detail, Orange, NetworkHelper.IsValidPortalUrl(_config.PortalUrl) ? "● 需要认证" : "● 未配置认证页");
                    _stripLabel.Text = "未联网。" + (string.IsNullOrEmpty(result.Error) ? "" : "");
                    if (manual) Logger.Warn("网络检测：未联网。" + result.Detail);

                    if (allowAuto)
                    {
                        MaybeAutoAuth(false);
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("网络检测异常：" + ex.Message);
                SetConnectionState("检测失败", ex.Message, Red, "● 检测失败");
                return null;
            }
            finally
            {
                _checking = false;
            }
        }

        private void MaybeAutoAuth(bool initial)
        {
            if (!_config.AutoMonitor || _authInProgress) return;
            if (InSuccessGrace()) return;
            if (initial && _startedByAutostart && !_config.AutoLoginOnStartup) return;
            if (!NetworkHelper.IsValidPortalUrl(_config.PortalUrl)) return;

            double cooldown = 60;
            if (_autoFailures > 0)
            {
                cooldown = Math.Min(600, 60 * Math.Pow(2, Math.Min(_autoFailures, 3)));
            }
            if ((DateTime.UtcNow - _lastAutoAttempt).TotalSeconds < cooldown) return;
            TriggerAutoAuth();
        }

        private bool InSuccessGrace()
        {
            return _lastAuthSuccessUtc != DateTime.MinValue &&
                (DateTime.UtcNow - _lastAuthSuccessUtc).TotalMinutes < 15;
        }

        private void TriggerAutoAuth()
        {
            if (_authInProgress) return;
            if (!NetworkHelper.IsValidPortalUrl(_config.PortalUrl))
            {
                Logger.Warn("自动认证已跳过：Portal 地址未配置。");
                return;
            }
            _lastAutoAttempt = DateTime.UtcNow;
            Logger.Info("开始自动认证。");
            StartAuth(true, false);
        }

        private void StartAuth(bool auto, bool openOnly)
        {
            if (_authInProgress && _loginForm != null)
            {
                _loginForm.Reveal();
                return;
            }

            if (!SaveSettingsFromUi(false))
            {
                _tabs.SelectedIndex = 1;
                _urlBox.Focus();
                return;
            }

            if (!NetworkHelper.IsValidPortalUrl(_config.PortalUrl))
            {
                MessageBox.Show(this, "请先填写有效的 Portal 地址（http:// 或 https://）。", "QuickPortal",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _tabs.SelectedIndex = 1;
                _urlBox.Focus();
                return;
            }

            bool silent = auto && _config.SilentAuto && !openOnly;
            bool submit = openOnly ? false : _config.AutoSubmit;

            _authSucceeded = false;
            _authInProgress = true;
            SetConnectionState("正在认证", "正在打开 Portal 认证页面" + (silent ? "（静默）" : "") + "。", Primary, "● 正在认证");
            _stripLabel.Text = "正在认证...";

            _loginForm = new LoginForm(_config, submit, silent);
            _loginForm.Succeeded += OnLoginSucceeded;
            _loginForm.Failed += OnLoginFailed;
            _loginForm.FormClosed += delegate
            {
                _loginForm = null;
                _authInProgress = false;
                if (!_authSucceeded)
                {
                    Logger.Info("Portal 认证窗口已关闭。");
                    SetConnectionState("需要认证", "认证窗口已关闭，可随时重新尝试。", Orange, "● 需要认证");
                }
            };
            _loginForm.Show();
            if (!silent)
            {
                _loginForm.Activate();
            }
        }

        private void OnLoginSucceeded(LoginForm form)
        {
            _authSucceeded = true;
            _autoFailures = 0;
            _lastAutoAttempt = DateTime.MinValue;
            _lastAuthSuccessUtc = DateTime.UtcNow;
            _config.LastAuthUtc = DateTime.UtcNow.ToString("o");
            ConfigStore.Save(_config);
            SetConnectionState("已联网", "Portal 认证成功，互联网连接正常。", Green, "● 已联网");
            _stripLabel.Text = "认证成功。";
            UpdateLastAuthLabel();
            ShowBalloon("认证成功", "QuickPortal 已完成 Portal 认证。", ToolTipIcon.Info);
        }

        private void OnLoginFailed(LoginForm form, string message)
        {
            _autoFailures++;
            Logger.Warn("认证未完成：" + message);
            if (form != null && form.IsSilent)
            {
                // Silent attempts must stay silent. Avoid balloon spam and never
                // force the login window to the foreground for a probe failure.
                return;
            }
            ShowBalloon("认证未完成", message + " 请打开主界面或认证窗口检查。", ToolTipIcon.Warning);
            if (!_config.SilentAuto)
            {
                SetConnectionState("认证未完成", message, Orange, "● 认证未完成");
            }
        }

        private void AuthNow()
        {
            ShowMainWindowIfHiddenForManual();
            StartAuth(false, false);
        }

        private void OpenPortalOnly()
        {
            ShowMainWindowIfHiddenForManual();
            StartAuth(false, true);
        }

        private void ShowMainWindowIfHiddenForManual()
        {
            if (!Visible)
            {
                ShowMainWindow();
            }
        }

        private bool SaveSettingsFromUi(bool showMessage)
        {
            string url = NetworkHelper.NormalizeUrl(_urlBox.Text);
            if (url.Length > 0 && !NetworkHelper.IsValidPortalUrl(url))
            {
                MessageBox.Show(this, "Portal 地址格式不正确，请填写 http:// 或 https:// 开头的完整地址。", "QuickPortal",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _tabs.SelectedIndex = 1;
                _urlBox.Focus();
                return false;
            }

            _urlBox.Text = url;
            _config.PortalUrl = url;
            _config.Username = _userBox.Text.Trim();
            _config.SetPassword(_pwdBox.Text);
            _config.AutoFill = _autoFill.Checked;
            _config.AutoSubmit = _autoSubmit.Checked;

            bool saved = ConfigStore.Save(_config);
            if (!saved)
            {
                MessageBox.Show(this, "保存设置失败，请检查数据目录是否可写：\r\n" + ConfigStore.DataDir, "QuickPortal",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            if (showMessage)
            {
                Logger.Info("认证设置已保存。");
            }
            return true;
        }

        private void SaveQuiet()
        {
            if (!ConfigStore.Save(_config))
            {
                _stripLabel.Text = "配置保存失败。";
            }
        }

        private async Task DetectPortalAsync()
        {
            _detectButton.Enabled = false;
            _detectButton.Text = "探测中...";
            try
            {
                DetectResult result = await NetworkHelper.DetectPortalAsync();
                if (!string.IsNullOrEmpty(result.PortalUrl))
                {
                    _urlBox.Text = result.PortalUrl;
                    _stripLabel.Text = "已找到 Portal 地址，请确认后保存。";
                    Logger.Info("自动探测到 Portal 地址：" + result.PortalUrl);
                }
                else
                {
                    _stripLabel.Text = result.Detail;
                    MessageBox.Show(this, result.Detail, "自动探测", MessageBoxButtons.OK,
                        result.Online ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "自动探测失败：" + ex.Message, "自动探测", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _detectButton.Enabled = true;
                _detectButton.Text = "自动探测";
            }
        }

        private void OnAutostartCheckChanged(CheckBox source, CheckBox other)
        {
            if (_loading) return;
            bool enabled = source.Checked;
            try
            {
                AutostartResult result = Autostart.SetEnabled(enabled);
                if (!result.Success)
                {
                    _loading = true;
                    source.Checked = !enabled;
                    if (other != null) other.Checked = !enabled;
                    _loading = false;
                    UpdateAutostartInfo();
                    MessageBox.Show(this, "设置开机自启失败：\r\n" +
                        (string.IsNullOrEmpty(result.Message) ? "注册表和启动文件夹均不可写。" : result.Message) +
                        "\r\n\r\n请以管理员身份运行 QuickPortal 后重试。", "QuickPortal",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _loading = true;
                if (other != null) other.Checked = enabled;
                if (_quickAutoStart != null) _quickAutoStart.Checked = enabled;
                if (_startWithWindows != null) _startWithWindows.Checked = enabled;
                if (_trayAutoStart != null) _trayAutoStart.Checked = enabled;
                _loading = false;
                UpdateAutostartInfo();
                _stripLabel.Text = result.Message;
                Logger.Info(result.Message);
            }
            catch (Exception ex)
            {
                _loading = true;
                source.Checked = !enabled;
                if (other != null) other.Checked = !enabled;
                _loading = false;
                UpdateAutostartInfo();
                MessageBox.Show(this, "设置开机自启失败：" + ex.Message +
                    "\r\n\r\n请以管理员身份运行 QuickPortal 后重试。", "QuickPortal",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyAutostartFromTray(bool enabled)
        {
            try
            {
                AutostartResult result = Autostart.SetEnabled(enabled);
                if (!result.Success)
                {
                    _loading = true;
                    _trayAutoStart.Checked = !enabled;
                    _loading = false;
                    UpdateAutostartInfo();
                    MessageBox.Show("设置开机自启失败：\r\n" +
                        (string.IsNullOrEmpty(result.Message) ? "注册表和启动文件夹均不可写。" : result.Message) +
                        "\r\n\r\n请以管理员身份运行 QuickPortal 后重试。", "QuickPortal",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _loading = true;
                _quickAutoStart.Checked = enabled;
                _startWithWindows.Checked = enabled;
                _trayAutoStart.Checked = enabled;
                _loading = false;
                UpdateAutostartInfo();
                _stripLabel.Text = result.Message;
                Logger.Info(result.Message);
            }
            catch (Exception ex)
            {
                _loading = true;
                _trayAutoStart.Checked = !enabled;
                _loading = false;
                UpdateAutostartInfo();
                MessageBox.Show("设置开机自启失败：" + ex.Message +
                    "\r\n\r\n请以管理员身份运行 QuickPortal 后重试。", "QuickPortal",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnAutoMonitorCheckChanged(CheckBox source, CheckBox other)
        {
            if (_loading) return;
            bool enabled = source.Checked;
            _config.AutoMonitor = enabled;
            _loading = true;
            if (other != null) other.Checked = enabled;
            _quickAutoMonitor.Checked = enabled;
            _autoMonitor.Checked = enabled;
            _loading = false;
            SaveQuiet();
            RestartMonitorTimer();
            _stripLabel.Text = enabled ? "已开启断网自动重连。" : "已关闭断网自动重连。";
        }

        private void OnAutoStartupCheckChanged(CheckBox source, CheckBox other)
        {
            if (_loading) return;
            bool enabled = source.Checked;
            _config.AutoLoginOnStartup = enabled;
            _loading = true;
            if (other != null) other.Checked = enabled;
            _quickStartupAuth.Checked = enabled;
            _autoStartup.Checked = enabled;
            _loading = false;
            SaveQuiet();
            _stripLabel.Text = enabled ? "已开启启动后自动认证。" : "已关闭启动后自动认证。";
        }

        private void SetConnectionState(string big, string detail, Color color, string header)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(delegate { SetConnectionState(big, detail, color, header); }));
                return;
            }
            _bigStatus.Text = big;
            _bigStatus.ForeColor = color;
            _statusDetail.Text = detail;
            _headerStatus.Text = header;
            _headerStatus.ForeColor = color;
            try
            {
                string trayText = "QuickPortal - " + big;
                if (trayText.Length > 60) trayText = trayText.Substring(0, 60);
                if (_tray != null) _tray.Text = trayText;
            }
            catch { }
        }

        private void UpdateLastAuthLabel()
        {
            if (string.IsNullOrEmpty(_config.LastAuthUtc))
            {
                _lastAuthLabel.Text = "上次认证：—";
                return;
            }
            DateTime utc;
            if (DateTime.TryParse(_config.LastAuthUtc, null, DateTimeStyles.RoundtripKind, out utc))
            {
                _lastAuthLabel.Text = "上次认证：" + utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                _lastAuthLabel.Text = "上次认证：—";
            }
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            if (!_config.Notify || _tray == null) return;
            try
            {
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = text;
                _tray.BalloonTipIcon = icon;
                _tray.ShowBalloonTip(3500);
            }
            catch { }
        }

        private void ShowTrayHint()
        {
            if (_trayHintShown) return;
            _trayHintShown = true;
            ShowBalloon("QuickPortal 仍在运行", "程序已最小化到系统托盘，双击图标可重新打开。", ToolTipIcon.Info);
        }

        public void ShowMainWindow()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(ShowMainWindow));
                return;
            }
            ShowInTaskbar = true;
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void ExitApplication()
        {
            _exiting = true;
            Close();
        }

        private void AppendLogLine(string line, LogLevel level)
        {
            if (IsDisposed) return;
            try
            {
                if (!IsHandleCreated) return;
                BeginInvoke(new Action(delegate
                {
                    if (IsDisposed || _logRich == null) return;
                    Color color = Color.FromArgb(203, 213, 225);
                    switch (level)
                    {
                        case LogLevel.Success: color = Color.FromArgb(134, 239, 172); break;
                        case LogLevel.Warn: color = Color.FromArgb(253, 224, 71); break;
                        case LogLevel.Error: color = Color.FromArgb(252, 165, 165); break;
                    }
                    if (_logRich.TextLength > 250000)
                    {
                        _logRich.Clear();
                    }
                    _logRich.SelectionStart = _logRich.TextLength;
                    _logRich.SelectionLength = 0;
                    _logRich.SelectionColor = color;
                    _logRich.AppendText(line + Environment.NewLine);
                    _logRich.SelectionColor = _logRich.ForeColor;
                    _logRich.ScrollToCaret();

                    if (_recentBox != null)
                    {
                        _recentBox.AppendText(line + Environment.NewLine);
                        if (_recentBox.TextLength > 9000)
                        {
                            _recentBox.Text = _recentBox.Text.Substring(_recentBox.TextLength - 5000);
                        }
                        _recentBox.SelectionStart = _recentBox.TextLength;
                        _recentBox.ScrollToCaret();
                    }
                }));
            }
            catch { }
        }

        private void LoadRecentLog(bool replace)
        {
            try
            {
                string file = Logger.CurrentLogFile;
                if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;
                string text = File.ReadAllText(file, Encoding.UTF8);
                if (text.Length > 16000) text = text.Substring(text.Length - 16000);
                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                StringBuilder tail = new StringBuilder();
                int start = Math.Max(0, lines.Length - 90);
                for (int i = start; i < lines.Length; i++)
                {
                    if (lines[i].Length == 0) continue;
                    tail.AppendLine(lines[i]);
                }
                if (replace)
                {
                    _logRich.Text = tail.ToString();
                    _recentBox.Text = tail.ToString();
                }
                else if (_logRich.TextLength == 0)
                {
                    _logRich.Text = tail.ToString();
                    _recentBox.Text = tail.ToString();
                }
            }
            catch { }
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_exiting && e.CloseReason == CloseReason.UserClosing && _config.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                ShowTrayHint();
                return;
            }
            _exiting = true;
            if (_monitorTimer != null) _monitorTimer.Stop();
            if (_networkDebounce != null) _networkDebounce.Stop();
            if (_trayBalloonTimer != null) _trayBalloonTimer.Stop();
            if (_loginForm != null)
            {
                try { _loginForm.Close(); } catch { }
            }
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            try { Logger.Logged -= AppendLogLine; } catch { }
            try { NetworkChange.NetworkAddressChanged -= OnNetworkChanged; } catch { }
            try { NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged; } catch { }
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            if (_trayMenu != null) _trayMenu.Dispose();
            if (_logoImage != null) _logoImage.Dispose();
            if (_monitorTimer != null) _monitorTimer.Dispose();
            if (_networkDebounce != null) _networkDebounce.Dispose();
            if (_trayBalloonTimer != null) _trayBalloonTimer.Dispose();
            ConfigStore.Save(_config);
            Application.Exit();
        }
    }
}

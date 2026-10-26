// -------------------------------------------------------------------------
// QuickPortal - Quick Portal authentication helper
// Copyright (C) 2026 QuickPortal contributors
// Licensed under the GNU General Public License v3.0 or later (see COPYING).
// -------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace QuickPortal
{
    public class LoginForm : Form
    {
        private readonly AppConfig _config;
        private readonly bool _autoSubmit;
        private bool _silent;
        private bool _success;
        private bool _webViewReady;
        private int _navigationCount;
        private WebView2 _webView;
        private Label _statusLabel;
        private Label _footerLabel;
        private Button _checkButton;
        private Button _retryButton;
        private Button _browserButton;
        private CancellationTokenSource _pollCts;
        private System.Windows.Forms.Timer _closeTimer;

        public event Action<LoginForm> Succeeded;
        public event Action<LoginForm, string> Failed;

        public bool IsSilent
        {
            get { return _silent; }
        }

        public LoginForm(AppConfig config, bool autoSubmit, bool silent)
        {
            _config = config;
            _autoSubmit = autoSubmit;
            _silent = silent;

            Text = "Portal 认证 - QuickPortal";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1020, 760);
            MinimumSize = new Size(820, 600);
            BackColor = Color.White;
            ShowIcon = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Font = new Font("Microsoft YaHei UI", 10.5F);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 3;
            layout.ColumnCount = 1;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            Controls.Add(layout);

            Panel top = new Panel();
            top.Dock = DockStyle.Fill;
            top.BackColor = Color.FromArgb(248, 250, 252);
            top.Padding = new Padding(12, 7, 12, 7);
            layout.Controls.Add(top, 0, 0);

            Label title = new Label();
            title.Text = "Portal 认证";
            title.Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(15, 23, 42);
            title.AutoSize = true;
            title.Location = new Point(12, 12);
            top.Controls.Add(title);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Right;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.AutoSize = true;
            top.Controls.Add(actions);

            _browserButton = MakeTopButton("在浏览器中打开");
            _browserButton.Click += delegate
            {
                if (!NetworkHelper.OpenInBrowser(_config.PortalUrl))
                {
                    MessageBox.Show(this, "无法打开系统浏览器。", "QuickPortal", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            actions.Controls.Add(_browserButton);

            _retryButton = MakeTopButton("重新填充");
            _retryButton.Click += async delegate { await ReinjectAsync(); };
            actions.Controls.Add(_retryButton);

            _checkButton = MakeTopButton("检测联网");
            _checkButton.Click += delegate
            {
                SetStatus("正在检测网络...", Color.FromArgb(71, 85, 105));
                StartPolling();
            };
            actions.Controls.Add(_checkButton);

            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            _webView.DefaultBackgroundColor = Color.White;
            layout.Controls.Add(_webView, 0, 1);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Fill;
            bottom.BackColor = Color.FromArgb(248, 250, 252);
            layout.Controls.Add(bottom, 0, 2);

            _footerLabel = new Label();
            _footerLabel.Text = "准备中...";
            _footerLabel.Dock = DockStyle.Fill;
            _footerLabel.TextAlign = ContentAlignment.MiddleLeft;
            _footerLabel.Padding = new Padding(12, 0, 0, 0);
            _footerLabel.ForeColor = Color.FromArgb(100, 116, 139);
            _footerLabel.Font = new Font("Microsoft YaHei UI", 10F);
            bottom.Controls.Add(_footerLabel);

            _statusLabel = new Label();
            _statusLabel.Text = "正在启动内置浏览器...";
            _statusLabel.Dock = DockStyle.Top;
            _statusLabel.Height = 32;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.Padding = new Padding(12, 0, 0, 0);
            _statusLabel.BackColor = Color.FromArgb(239, 246, 255);
            _statusLabel.ForeColor = Color.FromArgb(30, 64, 175);
            layout.Controls.Add(_statusLabel, 0, 2);
            _statusLabel.BringToFront();

            // The status label shares the footer row with the subtle footer text.
            _statusLabel.Height = 32;
            _footerLabel.Visible = false;

            _closeTimer = new System.Windows.Forms.Timer();
            _closeTimer.Interval = 1200;
            _closeTimer.Tick += delegate
            {
                _closeTimer.Stop();
                Close();
            };

            Shown += async delegate { await InitializeWebViewAsync(); };
            FormClosing += OnFormClosing;
            FormClosed += delegate
            {
                if (_pollCts != null) _pollCts.Cancel();
            };

            if (_silent)
            {
                ShowInTaskbar = false;
                Opacity = 0;
                StartPosition = FormStartPosition.CenterScreen;
            }

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
        }

        protected override bool ShowWithoutActivation
        {
            get { return _silent; }
        }

        private Button MakeTopButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(51, 65, 85);
            button.Font = new Font("Microsoft YaHei UI", 10F);
            button.Margin = new Padding(6, 2, 0, 0);
            button.Padding = new Padding(8, 0, 8, 0);
            button.Cursor = Cursors.Hand;
            return button;
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                Directory.CreateDirectory(ConfigStore.WebViewDir);
                SetStatus("正在启动内置浏览器...", Color.FromArgb(71, 85, 105));
                CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                    null, ConfigStore.WebViewDir, null);
                await _webView.EnsureCoreWebView2Async(environment);

                _webViewReady = true;
                CoreWebView2 core = _webView.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                core.Settings.IsWebMessageEnabled = true;

                core.NavigationStarting += OnNavigationStarting;
                core.NavigationCompleted += OnNavigationCompleted;
                core.DOMContentLoaded += OnDomReady;
                core.FrameCreated += OnFrameCreated;
                core.WebMessageReceived += OnWebMessageReceived;
                core.ProcessFailed += OnProcessFailed;

                SetStatus("正在打开认证页面...", Color.FromArgb(71, 85, 105));
                _footerLabel.Visible = true;
                _footerLabel.Text = _config.PortalUrl;
                core.Navigate(_config.PortalUrl);
            }
            catch (WebView2RuntimeNotFoundException)
            {
                HandleBrowserFailure("未安装 Microsoft Edge WebView2 运行时。请安装 WebView2 Runtime 后重试，或在系统浏览器中完成认证。");
            }
            catch (Exception ex)
            {
                HandleBrowserFailure("内置浏览器启动失败：" + ex.Message);
            }
        }

        private void HandleBrowserFailure(string message)
        {
            Logger.Error(message);
            SetStatus("内置浏览器不可用", Color.FromArgb(185, 28, 28));
            Action<LoginForm, string> handler = Failed;
            if (handler != null) handler(this, message);
            if (_silent)
            {
                ScheduleClose(2000);
                return;
            }
            Reveal();
            MessageBox.Show(this, message, "QuickPortal", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            _navigationCount++;
            SetStatus("正在加载认证页面...", Color.FromArgb(71, 85, 105));
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                string detail = "认证页加载失败：" + e.WebErrorStatus;
                Logger.Warn(detail);
                SetStatus(detail, Color.FromArgb(185, 28, 28));
                Action<LoginForm, string> handler = Failed;
                if (handler != null) handler(this, detail);
                if (_silent) ScheduleClose(2500);
                else Reveal();
                return;
            }
            SetStatus("页面已加载，正在识别登录表单...", Color.FromArgb(71, 85, 105));
            BeginInvoke(new Action(async delegate
            {
                await Task.Delay(250);
                await InjectAsync();
                await Task.Delay(1200);
                await InjectAsync();
            }));
        }

        private void OnDomReady(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            BeginInvoke(new Action(async delegate
            {
                await Task.Delay(100);
                await InjectAsync();
            }));
        }

        private void OnFrameCreated(object sender, CoreWebView2FrameCreatedEventArgs e)
        {
            CoreWebView2Frame frame = e.Frame;
            frame.DOMContentLoaded += delegate
            {
                BeginInvoke(new Action(async delegate
                {
                    await Task.Delay(150);
                    await InjectIntoFrameAsync(frame);
                }));
            };
        }

        private async Task InjectAsync()
        {
            if (!_webViewReady || _webView.CoreWebView2 == null) return;
            try
            {
                string script = PortalScript.Build(_config, _autoSubmit);
                string result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
                if (result != null && result.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Logger.Warn("注入脚本返回：" + result);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("注入自动填充脚本失败：" + ex.Message);
            }
        }

        private async Task InjectIntoFrameAsync(CoreWebView2Frame frame)
        {
            try
            {
                if (frame == null) return;
                string script = PortalScript.Build(_config, _autoSubmit);
                await frame.ExecuteScriptAsync(script);
            }
            catch { }
        }

        private async Task ReinjectAsync()
        {
            if (!_webViewReady || _webView.CoreWebView2 == null) return;
            try
            {
                await _webView.CoreWebView2.ExecuteScriptAsync("try { window.__quickPortalState = null; } catch(e) {}");
                await InjectAsync();
                SetStatus("已重新尝试填充账号密码。", Color.FromArgb(30, 64, 175));
            }
            catch (Exception ex)
            {
                Logger.Warn("重新填充失败：" + ex.Message);
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string eventName = "";
            string message = "";
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
                if (data != null)
                {
                    if (data.ContainsKey("event")) eventName = Convert.ToString(data["event"]);
                    if (data.ContainsKey("message")) message = Convert.ToString(data["message"]);
                }
            }
            catch { return; }

            if (eventName == "filled")
            {
                Logger.Info("已识别登录表单：" + message);
                SetStatus(_autoSubmit ? "已自动填充，正在提交登录..." : "已自动填充，请手动点击登录按钮。",
                    Color.FromArgb(30, 64, 175));
                if (!_autoSubmit) RevealIfSilent();
            }
            else if (eventName == "submitted")
            {
                Logger.Info("已提交 Portal 登录：" + message);
                SetStatus("已提交登录，正在检测联网状态...", Color.FromArgb(30, 64, 175));
                StartPolling();
            }
            else if (eventName == "captcha")
            {
                Logger.Warn("检测到验证码，等待手动完成。");
                SetStatus("检测到验证码，请在窗口中手动完成认证。", Color.FromArgb(180, 83, 9));
                RevealIfSilent();
                StartPolling();
            }
            else if (eventName == "already")
            {
                Logger.Success("Portal 页面显示已登录：" + message);
                SetStatus("页面显示已登录，认证完成。", Color.FromArgb(21, 128, 61));
                OnAuthenticationSucceeded();
            }
            else if (eventName == "pageonline")
            {
                Logger.Success("Portal 页面显示认证成功。");
                SetStatus("认证成功，网络已连接。", Color.FromArgb(21, 128, 61));
                OnAuthenticationSucceeded();
            }
            else if (eventName == "noform")
            {
                Logger.Warn("未在页面中找到可自动填充的登录表单。");
                SetStatus("未找到登录表单，请在窗口中手动操作。", Color.FromArgb(180, 83, 9));
                Action<LoginForm, string> handler = Failed;
                if (handler != null) handler(this, "未找到可自动填充的登录表单。");
                if (_silent) ScheduleClose(2000);
                else Reveal();
            }
        }

        private void OnProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            Logger.Error("内置浏览器进程异常：" + e.ProcessFailedKind);
            SetStatus("内置浏览器进程异常，请重试。", Color.FromArgb(185, 28, 28));
            Action<LoginForm, string> handler = Failed;
            if (handler != null) handler(this, "内置浏览器进程异常。");
            if (_silent) ScheduleClose(2000);
            else Reveal();
        }

        private void StartPolling()
        {
            if (_success) return;
            if (_pollCts != null) _pollCts.Cancel();
            _pollCts = new CancellationTokenSource();
            CancellationToken token = _pollCts.Token;
            Task.Run(async delegate
            {
                for (int i = 0; i < 35; i++)
                {
                    if (token.IsCancellationRequested) return;
                    try { await Task.Delay(1800, token); }
                    catch (OperationCanceledException) { return; }
                    if (token.IsCancellationRequested) return;

                    ProbeResult result = await NetworkHelper.CheckAsync();
                    if (token.IsCancellationRequested) return;
                    if (result.Online)
                    {
                        try
                        {
                            BeginInvoke(new Action(delegate { OnAuthenticationSucceeded(); }));
                        }
                        catch { }
                        return;
                    }
                }
                try
                {
                    BeginInvoke(new Action(delegate { OnAuthenticationTimeout(); }));
                }
                catch { }
            });
        }

        private void OnAuthenticationSucceeded()
        {
            if (_success) return;
            _success = true;
            Logger.Success("Portal 认证成功，网络已连通。");
            SetStatus("认证成功，网络已连接。", Color.FromArgb(21, 128, 61));
            _statusLabel.BackColor = Color.FromArgb(240, 253, 244);
            _checkButton.Enabled = false;
            _retryButton.Enabled = false;
            ScheduleClose(1200);
            Action<LoginForm> handler = Succeeded;
            if (handler != null) handler(this);
        }

        private void OnAuthenticationTimeout()
        {
            if (_success) return;
            Logger.Warn("登录已提交，但暂未检测到联网成功。");
            SetStatus("暂未检测到联网成功，请检查页面提示，或稍后点击“检测联网”。", Color.FromArgb(180, 83, 9));
            Action<LoginForm, string> handler = Failed;
            if (handler != null) handler(this, "提交登录后未检测到网络连通。");
            if (_silent)
            {
                // Never reveal a silent window just because a probe endpoint is
                // unreachable: that is what made a successful login pop up again
                // on every monitoring interval. Close quietly and let the next
                // network check decide.
                Logger.Info("静默认证未确认联网，关闭隐藏的认证窗口。");
                ScheduleClose(2000);
            }
        }

        private void ScheduleClose(int milliseconds)
        {
            if (_closeTimer == null) return;
            _closeTimer.Stop();
            _closeTimer.Interval = milliseconds;
            _closeTimer.Start();
        }

        private void SetStatus(string text, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(delegate { SetStatus(text, color); }));
                return;
            }
            _statusLabel.Text = text;
            _statusLabel.ForeColor = color;
            _footerLabel.Text = text;
            _footerLabel.ForeColor = color;
        }

        public void Reveal()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(Reveal));
                return;
            }
            if (!_silent && Visible && Opacity >= 1.0)
            {
                Activate();
                return;
            }
            _silent = false;
            ShowInTaskbar = true;
            Opacity = 1;
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            if (!Visible) Show();
            CenterToScreen();
            BringToFront();
            Activate();
        }

        private void RevealIfSilent()
        {
            if (_silent) Reveal();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_pollCts != null)
            {
                _pollCts.Cancel();
                _pollCts = null;
            }
            if (_closeTimer != null) _closeTimer.Stop();
        }
    }
}

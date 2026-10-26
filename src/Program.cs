// QuickPortal - 快速 Web / Portal 认证工具
// Copyright (C) 2026 QuickPortal contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace QuickPortal
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\QuickPortal.SingleInstance.v1", out createdNew))
            {
                if (!createdNew)
                {
                    if (!Native.ActivateExisting())
                    {
                        MessageBox.Show("QuickPortal 已经在运行，请查看系统托盘。", "QuickPortal",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                ServicePointManager.Expect100Continue = false;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    Logger.Error("界面异常：" + e.Exception.Message);
                    MessageBox.Show("程序遇到错误：\r\n" + e.Exception.Message, "QuickPortal",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    Logger.Error("未处理异常：" + (ex == null ? "未知错误" : ex.ToString()));
                };

                Logger.Init();

                bool startedByAutostart = false;
                if (args != null)
                {
                    foreach (string arg in args)
                    {
                        if (string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase))
                        {
                            startedByAutostart = true;
                        }
                    }
                }

                try
                {
                    AppConfig config = ConfigStore.Load();
                    Application.Run(new MainForm(config, startedByAutostart));
                }
                catch (Exception ex)
                {
                    Logger.Error("启动失败：" + ex);
                    MessageBox.Show("QuickPortal 启动失败：\r\n" + ex.Message, "QuickPortal",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                GC.KeepAlive(mutex);
            }
        }
    }
}

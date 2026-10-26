// -------------------------------------------------------------------------
// QuickPortal - Quick Portal authentication helper
// Copyright (C) 2026 QuickPortal contributors
// Licensed under the GNU General Public License v3.0 or later (see COPYING).
// -------------------------------------------------------------------------
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace QuickPortal
{
    public static class Native
    {
        public const string MainWindowTitle = "QuickPortal - 快速 Portal 认证";
        public const int SwRestore = 9;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string lpString);

        public static readonly int ShowMainMessage = RegisterWindowMessage("QuickPortal.ShowMain.v1");

        public static bool ActivateExisting()
        {
            for (int i = 0; i < 12; i++)
            {
                IntPtr handle = FindWindow(null, MainWindowTitle);
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SwRestore);
                    PostMessage(handle, ShowMainMessage, IntPtr.Zero, IntPtr.Zero);
                    SetForegroundWindow(handle);
                    return true;
                }
                Thread.Sleep(150);
            }
            return false;
        }
    }
}

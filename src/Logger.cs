// -------------------------------------------------------------------------
// QuickPortal - Quick Portal authentication helper
// Copyright (C) 2026 QuickPortal contributors
// Licensed under the GNU General Public License v3.0 or later (see COPYING).
// -------------------------------------------------------------------------
using System;
using System.IO;
using System.Text;

namespace QuickPortal
{
    public enum LogLevel
    {
        Info,
        Success,
        Warn,
        Error
    }

    public static class Logger
    {
        private static readonly object Sync = new object();
        private static string _logFile;
        public static event Action<string, LogLevel> Logged;

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(ConfigStore.LogDir);
                _logFile = Path.Combine(ConfigStore.LogDir,
                    "quickportal-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                CleanupOldLogs();
            }
            catch
            {
                _logFile = null;
            }
        }

        private static void CleanupOldLogs()
        {
            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-14);
                foreach (string file in Directory.GetFiles(ConfigStore.LogDir, "quickportal-*.log"))
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        public static void Info(string message) { Write(LogLevel.Info, message); }
        public static void Success(string message) { Write(LogLevel.Success, message); }
        public static void Warn(string message) { Write(LogLevel.Warn, message); }
        public static void Error(string message) { Write(LogLevel.Error, message); }

        public static void Write(LogLevel level, string message)
        {
            string tag;
            switch (level)
            {
                case LogLevel.Success: tag = "OK "; break;
                case LogLevel.Warn: tag = "WARN"; break;
                case LogLevel.Error: tag = "ERR "; break;
                default: tag = "INFO"; break;
            }

            string line = DateTime.Now.ToString("HH:mm:ss") + " [" + tag + "] " + (message ?? "");
            lock (Sync)
            {
                try
                {
                    if (!string.IsNullOrEmpty(_logFile))
                    {
                        File.AppendAllText(_logFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + tag + "] " + message + Environment.NewLine, Encoding.UTF8);
                    }
                }
                catch { }
            }

            Action<string, LogLevel> handler = Logged;
            if (handler != null)
            {
                try { handler(line, level); } catch { }
            }
        }

        public static string CurrentLogFile
        {
            get { return _logFile ?? ""; }
        }
    }
}

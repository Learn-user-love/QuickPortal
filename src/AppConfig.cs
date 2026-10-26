// -------------------------------------------------------------------------
// QuickPortal - Quick Portal authentication helper
// Copyright (C) 2026 QuickPortal contributors
// Licensed under the GNU General Public License v3.0 or later (see COPYING).
// -------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace QuickPortal
{
    public class AppConfig
    {
        public int Version { get; set; }
        public string PortalUrl { get; set; }
        public string Username { get; set; }
        public string PasswordCipher { get; set; }
        public bool AutoFill { get; set; }
        public bool AutoSubmit { get; set; }
        public bool AutoLoginOnStartup { get; set; }
        public bool AutoMonitor { get; set; }
        public bool SilentAuto { get; set; }
        public bool Notify { get; set; }
        public bool CloseToTray { get; set; }
        public int CheckIntervalSeconds { get; set; }
        public string CustomJs { get; set; }
        public string LastAuthUtc { get; set; }

        public AppConfig()
        {
            Version = 1;
            PortalUrl = "";
            Username = "";
            PasswordCipher = "";
            AutoFill = true;
            AutoSubmit = true;
            AutoLoginOnStartup = true;
            AutoMonitor = true;
            SilentAuto = true;
            Notify = true;
            CloseToTray = true;
            CheckIntervalSeconds = 30;
            CustomJs = "";
            LastAuthUtc = "";
        }

        [ScriptIgnore]
        public bool HasPassword
        {
            get { return !string.IsNullOrEmpty(PasswordCipher); }
        }

        public string GetPassword()
        {
            if (string.IsNullOrEmpty(PasswordCipher)) return "";
            try
            {
                byte[] entropy = Encoding.UTF8.GetBytes("QuickPortal.Config.v1");
                byte[] data = Convert.FromBase64String(PasswordCipher);
                byte[] plain = ProtectedData.Unprotect(data, entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch
            {
                return "";
            }
        }

        public void SetPassword(string password)
        {
            if (password == null) password = "";
            try
            {
                byte[] entropy = Encoding.UTF8.GetBytes("QuickPortal.Config.v1");
                byte[] plain = Encoding.UTF8.GetBytes(password);
                byte[] data = ProtectedData.Protect(plain, entropy, DataProtectionScope.CurrentUser);
                PasswordCipher = Convert.ToBase64String(data);
            }
            catch
            {
                PasswordCipher = "";
            }
        }

        public void Normalize()
        {
            if (Version <= 0) Version = 1;
            if (PortalUrl == null) PortalUrl = "";
            if (Username == null) Username = "";
            if (PasswordCipher == null) PasswordCipher = "";
            if (CustomJs == null) CustomJs = "";
            if (LastAuthUtc == null) LastAuthUtc = "";
            if (CheckIntervalSeconds < 10) CheckIntervalSeconds = 30;
            if (CheckIntervalSeconds > 3600) CheckIntervalSeconds = 3600;
        }
    }

    public static class ConfigStore
    {
        public static readonly string DataDir = ResolveDataDir();

        private static string ResolveDataDir()
        {
            System.Collections.Generic.List<string> candidates = new System.Collections.Generic.List<string>();

            string overrideDir = Environment.GetEnvironmentVariable("QUICKPORTAL_DATA");
            if (!string.IsNullOrEmpty(overrideDir))
            {
                try { candidates.Add(Path.GetFullPath(Environment.ExpandEnvironmentVariables(overrideDir))); }
                catch { }
            }

            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickPortal"));
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickPortal"));
            candidates.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QuickPortalData"));
            candidates.Add(Path.Combine(Path.GetTempPath(), "QuickPortal"));

            foreach (string candidate in candidates)
            {
                if (CanWriteTo(candidate)) return candidate;
            }

            // Last resort: the temp folder. If even this is unavailable, errors are
            // reported by the settings dialog instead of crashing the application.
            return candidates[candidates.Count - 1];
        }

        private static bool CanWriteTo(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static readonly string LogDir = Path.Combine(DataDir, "logs");
        public static readonly string WebViewDir = Path.Combine(DataDir, "WebView2");
        public static readonly string ConfigPath = Path.Combine(DataDir, "settings.json");

        public static AppConfig Load()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                if (!File.Exists(ConfigPath)) return new AppConfig();

                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                AppConfig config = serializer.Deserialize<AppConfig>(json);
                if (config == null) return new AppConfig();
                config.Normalize();
                return config;
            }
            catch (Exception ex)
            {
                Logger.Warn("读取配置失败，已使用默认配置：" + ex.Message);
                try
                {
                    if (File.Exists(ConfigPath))
                    {
                        string bad = ConfigPath + ".bad-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Copy(ConfigPath, bad, true);
                    }
                }
                catch { }
                return new AppConfig();
            }
        }

        public static bool Save(AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(config);
                string tmp = ConfigPath + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                if (File.Exists(ConfigPath))
                {
                    try { File.Replace(tmp, ConfigPath, null); }
                    catch
                    {
                        File.Copy(tmp, ConfigPath, true);
                        File.Delete(tmp);
                    }
                }
                else
                {
                    File.Move(tmp, ConfigPath);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("保存配置失败：" + ex.Message);
                return false;
            }
        }
    }
}

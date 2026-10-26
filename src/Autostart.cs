// -------------------------------------------------------------------------
// QuickPortal - Quick Portal authentication helper
// Copyright (C) 2026 QuickPortal contributors
// Licensed under the GNU General Public License v3.0 or later (see COPYING).
// -------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickPortal
{
    public enum AutostartMethod
    {
        None,
        RegistryUser,
        RegistryMachine,
        StartupShortcut,
        StartupCommand,
        CommonStartupShortcut,
        CommonStartupCommand
    }

    public sealed class AutostartResult
    {
        public bool Success;
        public AutostartMethod Method;
        public string Message;
    }

    public static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "QuickPortal";
        private const string ShortcutName = "QuickPortal.lnk";
        private const string CommandName = "QuickPortal.cmd";

        public static string Command
        {
            get { return "\"" + Application.ExecutablePath + "\" --autostart"; }
        }

        public static bool IsEnabled()
        {
            return GetActiveMethod() != AutostartMethod.None;
        }

        public static bool HasAnyEntry()
        {
            string ignored;
            if (ReadRegistry(false, out ignored)) return true;
            if (ReadRegistry(true, out ignored)) return true;
            return File.Exists(GetStartupPath(false, true)) ||
                   File.Exists(GetStartupPath(false, false)) ||
                   File.Exists(GetStartupPath(true, true)) ||
                   File.Exists(GetStartupPath(true, false));
        }

        public static AutostartMethod GetActiveMethod()
        {
            string value;
            if (ReadRegistry(false, out value) && string.Equals(value, Command, StringComparison.OrdinalIgnoreCase))
                return AutostartMethod.RegistryUser;

            if (File.Exists(GetStartupPath(false, true))) return AutostartMethod.StartupShortcut;
            if (File.Exists(GetStartupPath(false, false))) return AutostartMethod.StartupCommand;

            if (ReadRegistry(true, out value) && string.Equals(value, Command, StringComparison.OrdinalIgnoreCase))
                return AutostartMethod.RegistryMachine;

            if (File.Exists(GetStartupPath(true, true))) return AutostartMethod.CommonStartupShortcut;
            if (File.Exists(GetStartupPath(true, false))) return AutostartMethod.CommonStartupCommand;
            return AutostartMethod.None;
        }

        public static string MethodText(AutostartMethod method)
        {
            switch (method)
            {
                case AutostartMethod.RegistryUser: return "当前用户注册表";
                case AutostartMethod.RegistryMachine: return "系统注册表（管理员）";
                case AutostartMethod.StartupShortcut: return "启动文件夹快捷方式";
                case AutostartMethod.StartupCommand: return "启动文件夹脚本";
                case AutostartMethod.CommonStartupShortcut: return "公共启动文件夹快捷方式";
                case AutostartMethod.CommonStartupCommand: return "公共启动文件夹脚本";
                default: return "未启用";
            }
        }

        public static AutostartResult SetEnabled(bool enabled)
        {
            if (enabled) return Enable();
            return Disable();
        }

        private static AutostartResult Enable()
        {
            List<string> errors = new List<string>();
            string error;

            // Preferred: current-user Run key. This normally needs no admin rights.
            if (TryWriteRegistry(false, out error))
            {
                return Success(AutostartMethod.RegistryUser);
            }
            AddError(errors, "当前用户注册表", error);

            // Fallback 1: a shortcut in the current user's Startup folder.
            if (TryCreateShortcut(false, out error))
            {
                return Success(AutostartMethod.StartupShortcut);
            }
            AddError(errors, "当前用户启动文件夹（快捷方式）", error);

            // Fallback 2: machine Run key, available when the app is elevated.
            if (TryWriteRegistry(true, out error))
            {
                return Success(AutostartMethod.RegistryMachine);
            }
            AddError(errors, "系统注册表", error);

            // Fallback 3: common Startup folder shortcut, also requires elevation.
            if (TryCreateShortcut(true, out error))
            {
                return Success(AutostartMethod.CommonStartupShortcut);
            }
            AddError(errors, "公共启动文件夹（快捷方式）", error);

            // Fallback 4: startup .cmd files. These avoid WScript/COM but may
            // briefly show a console window during sign-in.
            if (TryCreateCommand(false, out error))
            {
                return Success(AutostartMethod.StartupCommand);
            }
            AddError(errors, "当前用户启动文件夹（脚本）", error);

            if (TryCreateCommand(true, out error))
            {
                return Success(AutostartMethod.CommonStartupCommand);
            }
            AddError(errors, "公共启动文件夹（脚本）", error);

            AutostartResult failed = new AutostartResult();
            failed.Success = false;
            failed.Method = AutostartMethod.None;
            failed.Message = string.Join("\r\n", errors.ToArray());
            return failed;
        }

        private static AutostartResult Disable()
        {
            List<string> errors = new List<string>();
            string error;

            if (!TryDeleteRegistry(false, out error)) AddError(errors, "当前用户注册表", error);
            if (!TryDeleteRegistry(true, out error)) AddError(errors, "系统注册表", error);
            if (!TryDeleteStartupFile(false, true, out error)) AddError(errors, "当前用户启动文件夹（快捷方式）", error);
            if (!TryDeleteStartupFile(false, false, out error)) AddError(errors, "当前用户启动文件夹（脚本）", error);
            if (!TryDeleteStartupFile(true, true, out error)) AddError(errors, "公共启动文件夹（快捷方式）", error);
            if (!TryDeleteStartupFile(true, false, out error)) AddError(errors, "公共启动文件夹（脚本）", error);

            if (HasAnyEntry())
            {
                AutostartResult failed = new AutostartResult();
                failed.Success = false;
                failed.Method = GetActiveMethod();
                failed.Message = "仍有启动项无法删除。\r\n" + string.Join("\r\n", errors.ToArray());
                return failed;
            }

            AutostartResult result = new AutostartResult();
            result.Success = true;
            result.Method = AutostartMethod.None;
            result.Message = "已关闭开机自启。";
            return result;
        }

        private static AutostartResult Success(AutostartMethod method)
        {
            AutostartResult result = new AutostartResult();
            result.Success = true;
            result.Method = method;
            result.Message = "已开启开机自启（" + MethodText(method) + "）。";
            return result;
        }

        private static void AddError(List<string> errors, string label, string error)
        {
            if (!string.IsNullOrEmpty(error)) errors.Add(label + "：" + error);
        }

        private static bool TryWriteRegistry(bool machine, out string error)
        {
            error = null;
            try
            {
                RegistryKey root = machine ? Registry.LocalMachine : Registry.CurrentUser;
                using (RegistryKey key = root.CreateSubKey(RunKey))
                {
                    if (key == null)
                    {
                        error = "无法打开启动项注册表。";
                        return false;
                    }
                    key.SetValue(ValueName, Command, RegistryValueKind.String);
                }

                string value;
                if (!ReadRegistry(machine, out value))
                {
                    error = "写入后无法读取启动项。";
                    return false;
                }
                if (!string.Equals(value, Command, StringComparison.OrdinalIgnoreCase))
                {
                    error = "写入后校验不一致。";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryDeleteRegistry(bool machine, out string error)
        {
            error = null;
            try
            {
                RegistryKey root = machine ? Registry.LocalMachine : Registry.CurrentUser;
                using (RegistryKey key = root.OpenSubKey(RunKey, true))
                {
                    if (key == null) return true;
                    if (key.GetValue(ValueName) == null) return true;
                    key.DeleteValue(ValueName, false);
                }
                string remaining;
                return !ReadRegistry(machine, out remaining);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool ReadRegistry(bool machine, out string value)
        {
            value = null;
            try
            {
                RegistryKey root = machine ? Registry.LocalMachine : Registry.CurrentUser;
                using (RegistryKey key = root.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    object raw = key.GetValue(ValueName);
                    if (raw == null) return false;
                    value = Convert.ToString(raw);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string GetStartupPath(bool common, bool shortcut)
        {
            string folder = Environment.GetFolderPath(common
                ? Environment.SpecialFolder.CommonStartup
                : Environment.SpecialFolder.Startup);
            return Path.Combine(folder, shortcut ? ShortcutName : CommandName);
        }

        private static bool TryCreateShortcut(bool common, out string error)
        {
            error = null;
            string path = GetStartupPath(common, true);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                CreateShortcut(path);
                if (!File.Exists(path))
                {
                    error = "快捷方式创建后不存在。";
                    return false;
                }
                TryDeleteFileQuiet(GetStartupPath(common, false));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryCreateCommand(bool common, out string error)
        {
            error = null;
            string path = GetStartupPath(common, false);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string content = "@echo off\r\n" +
                    "start \"\" \"" + Application.ExecutablePath + "\" --autostart\r\n" +
                    "exit /b 0\r\n";
                File.WriteAllText(path, content, Encoding.Default);
                TryDeleteFileQuiet(GetStartupPath(common, true));
                return File.Exists(path);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryDeleteStartupFile(bool common, bool shortcut, out string error)
        {
            error = null;
            try
            {
                string path = GetStartupPath(common, shortcut);
                if (File.Exists(path)) File.Delete(path);
                return !File.Exists(path);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void TryDeleteFileQuiet(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private static void CreateShortcut(string path)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new InvalidOperationException("Windows Script Host 不可用。");

            object shell = null;
            object shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod,
                    null, shell, new object[] { path });
                Type shortcutType = shortcut.GetType();
                SetProperty(shortcutType, shortcut, "TargetPath", Application.ExecutablePath);
                SetProperty(shortcutType, shortcut, "Arguments", "--autostart");
                SetProperty(shortcutType, shortcut, "WorkingDirectory", Path.GetDirectoryName(Application.ExecutablePath));
                SetProperty(shortcutType, shortcut, "IconLocation", Application.ExecutablePath + ",0");
                SetProperty(shortcutType, shortcut, "Description", "QuickPortal 快速 Portal 认证");
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                ReleaseCom(shortcut);
                ReleaseCom(shell);
            }
        }

        private static void SetProperty(Type type, object target, string name, string value)
        {
            type.InvokeMember(name, BindingFlags.SetProperty, null, target, new object[] { value });
        }

        private static void ReleaseCom(object value)
        {
            try
            {
                if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
            }
            catch { }
        }
    }
}

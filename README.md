# QuickPortal（快速 Web / Portal 认证）

Windows 10/11 桌面小工具：一键打开 Portal 登录页，自动填充账号密码并点击登录，支持开机自启和断网自动重连。

> 当前版本：v1.0.5<br>
> 技术栈：C# / .NET Framework 4.8 / WinForms / Microsoft Edge WebView2

## 功能

- 内置 Edge WebView2 打开 Portal，避免来回切换浏览器
- 自动识别账号、密码输入框并填入
- 自动寻找并点击“登录 / 认证 / 连接 / Login / Sign in”按钮
- 自动探测当前网络的 Portal 跳转地址
- 多 HTTP/HTTPS 探测地址并行判断联网状态，减少误判
- 识别页面上的“登录成功 / 注销 / 已连接”等状态，作为联网判断的补充
- 静默自动认证失败时不会反复弹窗
- 开机自启（注册表、启动文件夹、系统启动项多路径回退）
- 断网自动重连、系统托盘、日志
- 密码使用 Windows DPAPI 加密保存

## 运行现成版本

双击：

```text
QuickPortal\QuickPortal.exe
```

请保留整个 `QuickPortal` 文件夹（exe 旁边的 3 个 WebView2 DLL 不能少）。不要只复制 exe。

系统要求：

- Windows 10/11
- .NET Framework 4.8（Windows 11 自带）
- Microsoft Edge WebView2 Runtime（新版 Windows 11 基本已自带）

## 首次使用

1. 打开“认证设置”。
2. 填写 **Portal 登录页地址**（通常就是连上 Wi-Fi 后浏览器自动跳转到的网页，例如 `http://10.0.0.55/a79.htm`）。
3. 填写账号和密码。
4. 保持勾选“自动填充账号密码”和“自动点击登录 / 认证”，点“保存设置”。
5. 回到“概览”，点“立即快速认证”。

不知道 Portal 地址时，可以点“自动探测”尝试从当前网络获取。

## 开机自启与自动认证

在“自动化”页：

- **开机自动启动 QuickPortal**：优先写入当前用户注册表；如果注册表被策略或安全软件限制，会自动尝试启动文件夹快捷方式、系统注册表、公共启动文件夹、启动脚本。界面会显示最终采用的方式。
- **Windows 启动后自动尝试认证**：开机启动后若检测到未联网，自动开始认证。
- **静默认证**：自动认证时不弹出窗口；只有遇到验证码或需要手动操作时才显示窗口，检测失败不会反复弹窗。
- **断网自动重连**：定时检测网络，断网时自动尝试认证。
- **关闭主窗口时最小化到系统托盘**：默认开启；退出请使用托盘菜单里的“退出 QuickPortal”。

移动程序位置后，需要重新勾选一次“开机自启”，让启动项指向新路径。

## 工作原理

1. 通过多个 HTTP/HTTPS 探测地址并行判断是否已联网，避免单个地址被 Portal 拦截导致误判。
2. 使用内置 Edge WebView2 打开 Portal 页面。
3. 注入 JavaScript，识别密码框和账号输入框，触发原生 `input/change` 事件后填入账号密码。
4. 自动寻找登录按钮并点击；遇到验证码、短信码或特殊控件时停止自动提交。
5. 登录提交后同时检查联网探测结果和页面状态，成功后关闭认证窗口并通知。

程序不绕过任何认证，只替代“打开网页、输入账号、点登录”的重复操作。Portal 页面自身的加密、验证码、SSO 流程仍然照常运行。

## 数据与安全

- 密码使用 Windows DPAPI（CurrentUser）加密，只有当前 Windows 用户能解密。
- 默认保存到 `%APPDATA%\QuickPortal\settings.json`。
- 如果目录不可写，会依次尝试 `%LOCALAPPDATA%\QuickPortal`、程序目录下的 `QuickPortalData`、系统临时目录。
- 可通过环境变量 `QUICKPORTAL_DATA` 指定数据目录。
- 日志位于数据目录的 `logs` 文件夹，保留最近 14 天。

## 项目结构

```text
QuickPortal/
├─ src/
│  ├─ Program.cs                 程序入口、单实例、全局异常
│  ├─ MainForm.cs                主窗口逻辑、认证调度、托盘、配置
│  ├─ MainForm.Designer.cs       主窗口控件布局
│  ├─ MainForm.resx              主窗口资源文件
│  ├─ LoginForm.cs               WebView2 认证窗口
│  ├─ PortalScript.cs            注入 Portal 的自动填充/点击脚本
│  ├─ NetworkHelper.cs           多端点联网探测、Portal 地址探测
│  ├─ AppConfig.cs               配置、DPAPI、数据目录回退
│  ├─ Autostart.cs               多路径开机自启
│  ├─ Logger.cs                  日志
│  ├─ Native.cs                  Win32 互操作、单实例激活
│  ├─ AssemblyInfo.cs            程序集信息
│  ├─ App.config                 .NET 4.8 / DPI 配置
│  ├─ app.manifest               PerMonitorV2、Win10/11 兼容
│  └─ QuickPortal.csproj         Visual Studio 项目
├─ assets/                       图标
├─ build.ps1                     离线构建脚本
├─ QuickPortal.sln               Visual Studio 解决方案
├─ COPYING                       GPL-3.0
└─ README.md
```

> 说明：`MainForm.Designer.cs` 中的界面由代码动态构建，因此 Visual Studio 窗体设计器可能无法可视化编辑，但项目可以直接编译运行。后续如需可视化编辑，可以把控件树逐步迁移成设计器生成的写法。

## 构建

### 方式一：Visual Studio

用 Visual Studio 2022 打开 `QuickPortal.sln`，还原 NuGet 后编译。需要安装“.NET Framework 4.8 开发工具”。

项目引用 `Microsoft.Web.WebView2` NuGet 包（1.0.2365.46）。

### 方式二：离线构建脚本

`build.ps1` 使用 Windows 自带的 .NET Framework 4.8 C# 编译器，不需要安装 Visual Studio。构建前需要本机存在 WebView2 托管 DLL；脚本默认读取 Microsoft Office 自带的 1.0.2365.46 版本，可修改脚本中的 `$webViewBin`。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

生成：

- `dist\QuickPortal\QuickPortal.exe`
- `dist\QuickPortal-1.0.5-win-x64.zip`

## 已知限制

- 不同厂商的 Portal 页面差异很大。程序使用通用表单识别，覆盖常见的“账号 + 密码 + 登录按钮”页面；非标准控件、验证码、短信、企业 SSO、二维码需要手动完成或针对页面定制。
- 如果 Portal 地址不可访问、DNS 异常或网络完全断开，程序无法凭空完成认证。
- 开机自启只对当前 Windows 用户生效；切换用户后需要在该用户下重新设置。
- 当前版本未做代码签名，Windows SmartScreen 可能提示“未知发布者”。

## 开源协议

本项目以 **GNU General Public License v3.0（GPL-3.0）或更高版本** 发布。

- 协议全文见仓库根目录的 [COPYING](COPYING)（即 GPL-3.0 官方原文）
- 协议原文：<https://www.gnu.org/licenses/gpl-3.0.html>

这意味着：

- 你可以自由使用、修改、再分发本项目
- 如果你分发修改后的程序，**必须同时提供完整源代码**，并继续使用 GPL-3.0（或更高版本）
- 分发时必须保留版权声明和本协议
- 本程序按“现状”提供，不附带任何担保

版权声明：

```text
Copyright (C) 2026 QuickPortal contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
```

> 程序使用 Microsoft WebView2（闭源第三方组件，按其自身许可分发，不属于本项目代码），托管层 `Microsoft.Web.WebView2` NuGet 包为 BSD 风格许可，与 GPL-3.0 兼容。

## 贡献

欢迎提交 Issue / Pull Request。适配新的 Portal 页面时，建议附上：

- 登录页 URL（可脱敏）
- 页面截图
- 输入框和登录按钮的 HTML 片段

不要提交真实账号、密码或可识别个人身份的信息。

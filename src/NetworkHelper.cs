// -------------------------------------------------------------------------
// QuickPortal - Quick Portal authentication helper
// Copyright (C) 2026 QuickPortal contributors
// Licensed under the GNU General Public License v3.0 or later (see COPYING).
// -------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Cache;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuickPortal
{
    public class ProbeResult
    {
        public bool Online { get; set; }
        public string Detail { get; set; }
        public string DetectedPortalUrl { get; set; }
        public int LatencyMs { get; set; }
        public string Error { get; set; }
    }

    public class DetectResult
    {
        public bool Online { get; set; }
        public string PortalUrl { get; set; }
        public string Detail { get; set; }
    }

    public static class NetworkHelper
    {
        public const string ProbeUrl = "http://www.msftconnecttest.com/connecttest.txt";
        public const string ProbeExpected = "Microsoft Connect Test";

        public static string NormalizeUrl(string url)
        {
            if (url == null) return "";
            url = url.Trim();
            if (url.Length == 0) return "";
            if (url.IndexOf("://", StringComparison.Ordinal) < 0) url = "http://" + url;
            return url;
        }

        public static bool IsValidPortalUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(NormalizeUrl(url), UriKind.Absolute, out uri)) return false;
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private sealed class ProbeEndpoint
        {
            public string Url;
            public string Expected;
            public ProbeEndpoint(string url, string expected)
            {
                Url = url;
                Expected = expected;
            }
        }

        // A captive portal can block or hijack any single detection endpoint.
        // Probe several well-known endpoints in parallel and treat the network as
        // online if any one of them returns its expected response.
        private static readonly ProbeEndpoint[] OnlineEndpoints = new ProbeEndpoint[]
        {
            new ProbeEndpoint("http://www.msftconnecttest.com/connecttest.txt", "Microsoft Connect Test"),
            new ProbeEndpoint("http://captive.apple.com/hotspot-detect.html", "Success"),
            new ProbeEndpoint("http://connect.rom.miui.com/generate_204", ""),
            new ProbeEndpoint("http://www.gstatic.com/generate_204", ""),
            new ProbeEndpoint("http://cp.cloudflare.com/generate_204", ""),
            new ProbeEndpoint("https://www.gstatic.com/generate_204", "")
        };

        public static async Task<ProbeResult> CheckAsync()
        {
            List<Task<ProbeResult>> tasks = new List<Task<ProbeResult>>();
            foreach (ProbeEndpoint endpoint in OnlineEndpoints)
            {
                ProbeEndpoint captured = endpoint;
                tasks.Add(Task.Run(() => CheckEndpoint(captured, 3500)));
            }

            ProbeResult[] results = await Task.WhenAll(tasks);
            ProbeResult captive = null;
            ProbeResult last = null;

            foreach (ProbeResult result in results)
            {
                if (result == null) continue;
                if (result.Online) return result;
                if (captive == null && !string.IsNullOrEmpty(result.DetectedPortalUrl)) captive = result;
                last = result;
            }

            if (captive != null) return captive;
            if (last != null) return last;
            return new ProbeResult { Detail = "未联网" };
        }

        private static ProbeResult CheckEndpoint(ProbeEndpoint endpoint, int timeoutMs)
        {
            ProbeResult result = new ProbeResult();
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                HttpWebRequest request = BuildRequest(endpoint.Url, timeoutMs);
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    int status = (int)response.StatusCode;
                    string body = ReadBody(response);
                    result.LatencyMs = (int)watch.ElapsedMilliseconds;

                    bool online = status == 204 ||
                        (status == 200 && !string.IsNullOrEmpty(endpoint.Expected) &&
                         body.IndexOf(endpoint.Expected, StringComparison.OrdinalIgnoreCase) >= 0);

                    if (online)
                    {
                        result.Online = true;
                        result.Detail = "已联网（" + result.LatencyMs + " ms）";
                        return result;
                    }

                    string location = response.Headers["Location"];
                    if (!string.IsNullOrEmpty(location))
                    {
                        result.DetectedPortalUrl = MakeAbsolute(location, endpoint.Url);
                    }
                    else if (status == 200 && body.Length > 20)
                    {
                        result.DetectedPortalUrl = endpoint.Url;
                    }
                    result.Detail = "网络需要 Portal 认证";
                    return result;
                }
            }
            catch (WebException ex)
            {
                result.LatencyMs = (int)watch.ElapsedMilliseconds;
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    try
                    {
                        string location = response.Headers["Location"];
                        if (!string.IsNullOrEmpty(location))
                        {
                            result.DetectedPortalUrl = MakeAbsolute(location, endpoint.Url);
                        }
                        result.Detail = "网络被拦截（HTTP " + (int)response.StatusCode + "），需要认证";
                        return result;
                    }
                    finally
                    {
                        response.Close();
                    }
                }

                result.Detail = "未联网";
                result.Error = ex.Message;
                if (ex.Status == WebExceptionStatus.NameResolutionFailure)
                {
                    result.Detail = "未联网（DNS 解析失败）";
                }
                else if (ex.Status == WebExceptionStatus.Timeout)
                {
                    result.Detail = "未联网（检测超时）";
                }
                else if (ex.Status == WebExceptionStatus.ConnectFailure)
                {
                    result.Detail = "未联网（无法连接网络）";
                }
                return result;
            }
            catch (Exception ex)
            {
                result.LatencyMs = (int)watch.ElapsedMilliseconds;
                result.Detail = "检测失败";
                result.Error = ex.Message;
                return result;
            }
        }

        public static Task<DetectResult> DetectPortalAsync()
        {
            return Task.Run(() =>
            {
                DetectResult result = new DetectResult();
                try
                {
                    HttpWebRequest request = BuildRequest(ProbeUrl, 7000);
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        int status = (int)response.StatusCode;
                        string location = response.Headers["Location"];
                        string body = ReadBody(response);
                        if (status == 200 && body.IndexOf(ProbeExpected, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.Online = true;
                            result.Detail = "当前已经联网，未发现需要认证的页面。";
                            return result;
                        }
                        if (!string.IsNullOrEmpty(location))
                        {
                            result.PortalUrl = MakeAbsolute(location, ProbeUrl);
                            result.Detail = "已找到 Portal 登录页。";
                            return result;
                        }
                        result.PortalUrl = ProbeUrl;
                        result.Detail = "探测地址被重定向到了认证页面。";
                        return result;
                    }
                }
                catch (WebException ex)
                {
                    HttpWebResponse response = ex.Response as HttpWebResponse;
                    if (response != null)
                    {
                        try
                        {
                            string location = response.Headers["Location"];
                            if (!string.IsNullOrEmpty(location))
                            {
                                result.PortalUrl = MakeAbsolute(location, ProbeUrl);
                                result.Detail = "已找到 Portal 登录页。";
                                return result;
                            }
                            result.PortalUrl = ProbeUrl;
                            result.Detail = "探测请求被拦截，请确认认证页地址。";
                            return result;
                        }
                        finally
                        {
                            response.Close();
                        }
                    }
                    result.Detail = "探测失败：" + ex.Message;
                    return result;
                }
                catch (Exception ex)
                {
                    result.Detail = "探测失败：" + ex.Message;
                    return result;
                }
            });
        }

        private static HttpWebRequest BuildRequest(string url, int timeoutMs)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.AllowAutoRedirect = false;
            request.Timeout = timeoutMs;
            request.ReadWriteTimeout = timeoutMs;
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) QuickPortal/1.0";
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            request.KeepAlive = false;
            return request;
        }

        private static string ReadBody(HttpWebResponse response)
        {
            try
            {
                using (System.IO.Stream stream = response.GetResponseStream())
                {
                    if (stream == null) return "";
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(stream, Encoding.UTF8, true))
                    {
                        char[] buffer = new char[2048];
                        int read = reader.Read(buffer, 0, buffer.Length);
                        if (read <= 0) return "";
                        return new string(buffer, 0, read);
                    }
                }
            }
            catch
            {
                return "";
            }
        }

        private static string MakeAbsolute(string location, string baseUrl)
        {
            try
            {
                Uri absolute;
                if (Uri.TryCreate(location, UriKind.Absolute, out absolute)) return absolute.ToString();
                Uri baseUri = new Uri(baseUrl);
                if (Uri.TryCreate(baseUri, location, out absolute)) return absolute.ToString();
            }
            catch { }
            return location;
        }

        public static string GetNetworkInfo()
        {
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable()) return "未连接网络";
                List<string> parts = new List<string>();
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    string ip = "";
                    foreach (UnicastIPAddressInformation addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            ip = addr.Address.ToString();
                            break;
                        }
                    }
                    if (ip.Length == 0) continue;

                    string kind = ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "以太网";
                    string ssid = "";
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        ssid = TryGetSsid();
                    }

                    string text = kind;
                    if (!string.IsNullOrEmpty(ssid)) text += " · " + ssid;
                    text += " · " + ip;
                    parts.Add(text);
                }

                if (parts.Count == 0) return "未连接网络";
                return string.Join(" / ", parts.ToArray());
            }
            catch
            {
                return "网络信息不可用";
            }
        }

        private static string TryGetSsid()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("netsh", "wlan show interfaces");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                using (Process process = Process.Start(psi))
                {
                    if (process == null) return "";
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(2000);
                    using (System.IO.StringReader reader = new System.IO.StringReader(output))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            Match match = Regex.Match(line, @"^\s*SSID\s*:\s*(.+?)\s*$", RegexOptions.IgnoreCase);
                            if (match.Success) return match.Groups[1].Value.Trim();
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        public static bool OpenInBrowser(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(NormalizeUrl(url)) { UseShellExecute = true });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool OpenFolder(string path)
        {
            try
            {
                if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

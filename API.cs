using GDMiniJSON;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using static Sarcary.API;

namespace Sarcary
{
    /// <summary>
    /// Sarcary Mod 的公共API接口
    /// 其他mod可以通过这个接口与Sarcary交互
    /// </summary>
    public static class API
    {
        // API版本
        public const string API_VERSION = "1.0.0";

        // 回调委托定义
        public delegate void ModRegisteredCallback(string modId, string modVersion);
        public delegate void UpdateAvailableCallback(string modId, ModUpdateInfo updateInfo);

        // 事件
        public static event ModRegisteredCallback OnModRegistered;
        public static event UpdateAvailableCallback OnUpdateAvailable;

        // 已注册的mod列表
        private static Dictionary<string, ModInfo> registeredMods = new Dictionary<string, ModInfo>();

        // 更新信息缓存
        private static Dictionary<string, ModUpdateInfo> updateInfos = new Dictionary<string, ModUpdateInfo>();

        // 更新检查器
        private static UpdateChecker updateChecker = new UpdateChecker();

        // GUI相关
        private static Dictionary<string, float> notificationTimes = new Dictionary<string, float>();
        private const float NOTIFICATION_DURATION = 10f;

        /// <summary>
        /// 注册一个mod到Sarcary系统
        /// </summary>
        /// <param name="modId">Mod的唯一ID</param>
        /// <param name="modVersion">Mod版本</param>
        /// <param name="requiredAPIVersion">需要的API版本</param>
        /// <returns>是否注册成功</returns>
        public static bool RegisterMod(string modId, string modVersion, string requiredAPIVersion = "1.0.0")
        {
            if (!Main.IsModActive())
            {
                Log.Error($"Cannot register mod {modId}: Sarcary is not active");
                return false;
            }

            if (!Main.Settings.enableAPI)
            {
                Log.Warning($"API is disabled. Mod {modId} registration ignored.");
                return false;
            }

            if (registeredMods.ContainsKey(modId))
            {
                Log.Warning($"Mod {modId} is already registered");
                return false;
            }

            // 检查API版本兼容性
            if (!IsVersionCompatible(requiredAPIVersion, API_VERSION))
            {
                Log.Error($"Mod {modId} requires API version {requiredAPIVersion}, but current is {API_VERSION}");
                return false;
            }

            var modInfo = new ModInfo
            {
                Id = modId,
                Version = modVersion,
                RegisteredTime = DateTime.Now,
                IsEnabled = true
            };

            registeredMods[modId] = modInfo;

            Log.Info($"Mod registered: {modId} v{modVersion}");

            // 触发事件
            OnModRegistered?.Invoke(modId, modVersion);

            // 自动检查更新（如果启用）
            if (Main.Settings.autoCheckUpdates && updateInfos.ContainsKey(modId))
            {
                CheckForUpdate(modId, modVersion);
            }

            return true;
        }

        /// <summary>
        /// 注册Mod并设置更新信息
        /// </summary>
        public static bool RegisterModWithUpdate(string modId, string modVersion,
            string updateCheckUrl = null, string updateDownloadUrl = null,
            string changelog = "", string requiredAPIVersion = "1.0.0")
        {
            bool success = RegisterMod(modId, modVersion, requiredAPIVersion);

            if (success)
            {
                // 保存更新信息
                var updateInfo = new ModUpdateInfo
                {
                    ModId = modId,
                    CurrentVersion = modVersion,
                    UpdateCheckUrl = updateCheckUrl,
                    UpdateDownloadUrl = updateDownloadUrl,
                    Changelog = changelog,
                    LastCheckTime = DateTime.MinValue,
                    IsUpdateAvailable = false  // 改为 IsUpdateAvailable
                };

                updateInfos[modId] = updateInfo;

                Log.Info($"Mod {modId} registered with update checking enabled");

                // 立即检查更新
                if (!string.IsNullOrEmpty(updateCheckUrl))
                {
                    CheckForUpdate(modId, modVersion);
                }
            }

            return success;
        }

        /// <summary>
        /// 注册更新信息
        /// </summary>
        public static void RegisterUpdate(string modId, string modVersion,
            string updateCheckUrl = null, string updateDownloadUrl = null,
            string changelog = "", string requiredAPIVersion = "1.0.0")
        {
            // 保存更新信息
            var updateInfo = new ModUpdateInfo
            {
                ModId = modId,
                CurrentVersion = modVersion,
                UpdateCheckUrl = updateCheckUrl,
                UpdateDownloadUrl = updateDownloadUrl,
                Changelog = changelog,
                LastCheckTime = DateTime.MinValue,
                IsUpdateAvailable = false  // 改为 IsUpdateAvailable
            };

            updateInfos[modId] = updateInfo;

            Log.Info($"Mod {modId} registered with update checking enabled");

            // 立即检查更新
            if (!string.IsNullOrEmpty(updateCheckUrl))
            {
                CheckForUpdate(modId, modVersion);
            }
        }

        /// <summary>
        /// 检查指定Mod的更新
        /// </summary>
        public static async void CheckForUpdate(string modId, string currentVersion)
        {
            if (!updateInfos.TryGetValue(modId, out var updateInfo))
            {
                Log.Warning($"No update info found for mod: {modId}");
                return;
            }

            try
            {
                Log.Info($"Checking for updates for mod: {modId}");

                var updateResult = await updateChecker.CheckUpdateAsync(updateInfo);

                if (updateResult.IsUpdateAvailable)  // 改为 IsUpdateAvailable
                {
                    Log.Info($"Update available for {modId}: {updateResult.LatestVersion}");

                    // 更新信息
                    updateInfos[modId].LatestVersion = updateResult.LatestVersion;
                    updateInfos[modId].IsUpdateAvailable = true;  // 改为 IsUpdateAvailable
                    updateInfos[modId].Changelog = updateResult.Changelog;
                    updateInfos[modId].UpdateDownloadUrl = updateResult.UpdateDownloadUrl;
                    updateInfos[modId].LastCheckTime = DateTime.Now;

                    // 设置通知显示时间
                    notificationTimes[modId] = Time.time;

                    // 触发更新可用事件
                    OnUpdateAvailable?.Invoke(modId, updateResult);
                }
                else
                {
                    Log.Info($"Mod {modId} is up to date");
                    updateInfos[modId].LastCheckTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to check update for {modId}: {ex.Message}");
            }
        }

        /// <summary>
        /// 强制检查所有已注册Mod的更新
        /// </summary>
        public static void CheckAllUpdates()
        {
            foreach (var mod in registeredMods.Values)
            {
                if (updateInfos.ContainsKey(mod.Id))
                {
                    CheckForUpdate(mod.Id, mod.Version);
                }
            }
        }

        /// <summary>
        /// 手动设置更新信息（用于不支持自动检查的Mod）
        /// </summary>
        public static bool SetUpdateInfo(string modId, string latestVersion,
            string downloadUrl, string changelog = "")
        {
            if (!registeredMods.ContainsKey(modId))
            {
                Log.Warning($"Cannot set update info: Mod {modId} is not registered");
                return false;
            }

            if (!updateInfos.TryGetValue(modId, out var updateInfo))
            {
                updateInfo = new ModUpdateInfo
                {
                    ModId = modId,
                    CurrentVersion = registeredMods[modId].Version
                };
            }

            updateInfo.LatestVersion = latestVersion;
            updateInfo.UpdateDownloadUrl = downloadUrl;
            updateInfo.Changelog = changelog;
            updateInfo.IsUpdateAvailable = true;  // 改为 IsUpdateAvailable
            updateInfo.LastCheckTime = DateTime.Now;

            updateInfos[modId] = updateInfo;

            // 设置通知显示时间
            notificationTimes[modId] = Time.time;

            // 触发事件
            OnUpdateAvailable?.Invoke(modId, updateInfo);

            return true;
        }

        /// <summary>
        /// 获取指定Mod的更新信息
        /// </summary>
        public static ModUpdateInfo GetUpdateInfo(string modId)
        {
            return updateInfos.TryGetValue(modId, out var info) ? info : null;
        }

        /// <summary>
        /// 获取所有有更新的Mod
        /// </summary>
        public static List<ModUpdateInfo> GetAvailableUpdates()
        {
            return updateInfos.Values
                .Where(info => info.IsUpdateAvailable)  // 改为 IsUpdateAvailable
                .ToList();
        }

        /// <summary>
        /// 打开指定Mod的更新下载页面
        /// </summary>
        public static void OpenUpdateDownload(string modId)
        {
            if (updateInfos.TryGetValue(modId, out var updateInfo) &&
                !string.IsNullOrEmpty(updateInfo.UpdateDownloadUrl))
            {
                Application.OpenURL(updateInfo.UpdateDownloadUrl);
            }
            else
            {
                Log.Warning($"No download URL available for mod: {modId}");
            }
        }

        /// <summary>
        /// 在GUI上绘制更新通知
        /// </summary>
        public static void DrawUpdateNotifications()
        {
            if (!Main.Settings.showUpdateNotifications)
                return;

            var updates = GetAvailableUpdates();
            if (updates.Count == 0)
                return;

            float yPos = 80f;
            float width = 350f;
            float height = 100f;

            foreach (var update in updates)
            {
                if (!notificationTimes.TryGetValue(update.ModId, out var startTime))
                {
                    notificationTimes[update.ModId] = Time.time;
                    startTime = Time.time;
                }

                // 检查通知是否过期
                if (Time.time - startTime > NOTIFICATION_DURATION)
                {
                    continue;
                }

                // 计算透明度（最后2秒淡出）
                float remaining = NOTIFICATION_DURATION - (Time.time - startTime);
                float alpha = Mathf.Clamp01(remaining / 2f);

                DrawUpdateNotification(update, yPos, width, height, alpha);
                yPos += height + 10f;
            }
        }

        private static void DrawUpdateNotification(ModUpdateInfo update, float yPos, float width, float height, float alpha)
        {
            Rect rect = new Rect(Screen.width - width - 10, yPos, width, height);

            // 背景
            GUI.color = new Color(0.1f, 0.1f, 0.1f, alpha * 0.8f);
            GUI.Box(rect, "");
            GUI.color = Color.white;

            // 标题
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 14;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(1, 1, 0, alpha);
            titleStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Label(new Rect(rect.x + 10, rect.y + 5, rect.width - 20, 25),
                     $"<color=#FFFF00>●</color> {update.ModId} 有更新可用", titleStyle);

            // 版本信息
            GUIStyle versionStyle = new GUIStyle(GUI.skin.label);
            versionStyle.normal.textColor = new Color(1, 1, 1, alpha);

            GUI.Label(new Rect(rect.x + 10, rect.y + 30, rect.width - 20, 20),
                     $"v{update.CurrentVersion} → v{update.LatestVersion}", versionStyle);

            // 按钮
            float buttonWidth = 80f;
            float buttonHeight = 25f;
            float buttonY = rect.y + 55f;
            float buttonSpacing = 10f;

            GUI.color = new Color(1, 1, 1, alpha);

            // 下载按钮
            if (GUI.Button(new Rect(rect.x + 10, buttonY, buttonWidth, buttonHeight), "Download"))
            {
                OpenUpdateDownload(update.ModId);
                notificationTimes.Remove(update.ModId);
            }

            // 详情按钮
            if (GUI.Button(new Rect(rect.x + 10 + buttonWidth + buttonSpacing, buttonY, buttonWidth, buttonHeight), "Update Info"))
            {
                Log.Info($"[{update.ModId}] Update Info:\n{update.Changelog}");
            }

            // 忽略按钮
            if (GUI.Button(new Rect(rect.x + 10 + (buttonWidth + buttonSpacing) * 2, buttonY, buttonWidth, buttonHeight), "忽略"))
            {
                notificationTimes.Remove(update.ModId);
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// 取消注册一个mod
        /// </summary>
        public static bool UnregisterMod(string modId)
        {
            if (registeredMods.Remove(modId))
            {
                Log.Info($"Mod unregistered: {modId}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取Sarcary的版本信息
        /// </summary>
        public static string GetSarcaryVersion()
        {
            return Main.GetModVersion();
        }

        /// <summary>
        /// 获取所有注册的mod
        /// </summary>
        public static List<ModInfo> GetRegisteredMods()
        {
            return registeredMods.Values.ToList();
        }

        /// <summary>
        /// 检查mod是否已注册
        /// </summary>
        public static bool IsModRegistered(string modId)
        {
            return registeredMods.ContainsKey(modId);
        }

        /// <summary>
        /// 获取指定mod的信息
        /// </summary>
        public static ModInfo GetModInfo(string modId)
        {
            return registeredMods.TryGetValue(modId, out var info) ? info : null;
        }

        /// <summary>
        /// 检查版本兼容性
        /// </summary>
        private static bool IsVersionCompatible(string required, string current)
        {
            try
            {
                var requiredParts = required.Split('.').Select(int.Parse).ToArray();
                var currentParts = current.Split('.').Select(int.Parse).ToArray();

                // 主版本号必须匹配
                return requiredParts[0] == currentParts[0];
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Mod信息类
        /// </summary>
        public class ModInfo
        {
            public string Id { get; set; }
            public string Version { get; set; }
            public DateTime RegisteredTime { get; set; }
            public bool IsEnabled { get; set; }
        }

        /// <summary>
        /// Mod更新信息类
        /// </summary>
        public class ModUpdateInfo
        {
            public string ModId { get; set; }
            public string CurrentVersion { get; set; }
            public string LatestVersion { get; set; }
            public string UpdateCheckUrl { get; set; }
            public string UpdateDownloadUrl { get; set; }
            public string Changelog { get; set; }
            public bool IsUpdateAvailable { get; set; }  // 改为 IsUpdateAvailable
            public DateTime LastCheckTime { get; set; }

            public override string ToString()
            {
                return $"{ModId}: {CurrentVersion} -> {LatestVersion}";
            }
        }

        /// <summary>
        /// 更新检查器类
        /// </summary>
        private class UpdateChecker
        {
            public async Task<ModUpdateInfo> CheckUpdateAsync(ModUpdateInfo updateInfo)
            {
                try
                {
                    if (string.IsNullOrEmpty(updateInfo.UpdateCheckUrl))
                    {
                        Log.Debug($"No update check URL for {updateInfo.ModId}");
                        return updateInfo;
                    }

                    Log.Info($"Checking updates for {updateInfo.ModId} from {updateInfo.UpdateCheckUrl}");

                    using (var client = new WebClient())
                    {
                        client.Headers.Add("User-Agent", "Sarcary-UpdateChecker/1.0");
                        client.Encoding = Encoding.UTF8;

                        // 设置超时时间
                        client.DownloadStringCompleted += (sender, e) =>
                        {
                            if (e.Error != null)
                            {
                                Log.Warning($"Download error for {updateInfo.ModId}: {e.Error.Message}");
                            }
                        };

                        string response = await client.DownloadStringTaskAsync(updateInfo.UpdateCheckUrl);

                        if (string.IsNullOrEmpty(response))
                        {
                            Log.Warning($"Empty response for {updateInfo.ModId}");
                            return updateInfo;
                        }

                        // 清理响应文本
                        response = response.Trim();

                        Log.Debug($"Response for {updateInfo.ModId} (length: {response.Length}): {response}");

                        // 尝试多种方式解析版本号
                        string latestVersion = ExtractVersionFromResponse(response, updateInfo.ModId);

                        if (string.IsNullOrEmpty(latestVersion))
                        {
                            Log.Warning($"Could not extract version for {updateInfo.ModId}");
                            return updateInfo;
                        }

                        // 清理版本号
                        latestVersion = CleanVersion(latestVersion);
                        string currentVersion = CleanVersion(updateInfo.CurrentVersion);

                        Log.Info($"Version comparison for {updateInfo.ModId}: Current={currentVersion}, Latest={latestVersion}");

                        if (IsNewerVersion(latestVersion, currentVersion))
                        {
                            updateInfo.LatestVersion = latestVersion;
                            updateInfo.IsUpdateAvailable = true;

                            // 尝试提取更多信息
                            ExtractAdditionalInfo(response, updateInfo);

                            Log.Info($"✓ Update available for {updateInfo.ModId}: v{currentVersion} → v{latestVersion}");

                            // 自动生成下载链接（如果需要）
                            if (string.IsNullOrEmpty(updateInfo.UpdateDownloadUrl) &&
                                !string.IsNullOrEmpty(updateInfo.UpdateCheckUrl))
                            {
                                updateInfo.UpdateDownloadUrl = GenerateDownloadUrl(
                                    updateInfo.UpdateCheckUrl, latestVersion);
                            }
                        }
                        else
                        {
                            updateInfo.IsUpdateAvailable = false;
                            Log.Info($"✓ {updateInfo.ModId} is up to date (v{currentVersion})");
                        }

                        updateInfo.LastCheckTime = DateTime.Now;
                    }
                }
                catch (WebException webEx)
                {
                    if (webEx.Status == WebExceptionStatus.Timeout)
                    {
                        Log.Warning($"Update check timeout for {updateInfo.ModId}");
                    }
                    else
                    {
                        Log.Error($"Network error for {updateInfo.ModId}: {webEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Update check failed for {updateInfo.ModId}: {ex.Message}");
                }

                return updateInfo;
            }

            /// <summary>
            /// 从响应中提取版本号
            /// </summary>
            private string ExtractVersionFromResponse(string response, string modId)
            {
                try
                {
                    // 方法1：尝试解析为简单JSON {"version": "1.11.2"}
                    if (response.StartsWith("{") && response.EndsWith("}"))
                    {
                        try
                        {
                            // 使用更简单的JSON解析方法
                            var jsonData = ParseSimpleJson(response);
                            if (jsonData.ContainsKey("version"))
                            {
                                string version = jsonData["version"].Trim();
                                Log.Debug($"Found version in JSON: {version}");
                                return version;
                            }
                        }
                        catch (Exception jsonEx)
                        {
                            Log.Debug($"JSON parse failed: {jsonEx.Message}");
                        }
                    }

                    // 方法2：使用正则表达式查找版本号
                    // 匹配格式：1.11.2 或 v1.11.2 或 "version": "1.11.2"
                    var versionPatterns = new[]
                    {
                    @"""version""\s*:\s*""([^""]+)""",  // "version": "1.11.2"
                    @"""version""\s*:\s*'([^']+)'",    // 'version': '1.11.2'
                    @"version\s*=\s*""([^""]+)""",      // version="1.11.2"
                    @"version\s*=\s*'([^']+)'",        // version='1.11.2'
                    @"v?(\d+\.\d+(?:\.\d+)?(?:\.\d+)?)", // 1.11.2 或 v1.11.2
                    @"(\d+\.\d+\.\d+)",                // 1.11.2
                    @"(\d+\.\d+)"                      // 1.11
                };

                    foreach (var pattern in versionPatterns)
                    {
                        var match = Regex.Match(response, pattern, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string version = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(version))
                            {
                                Log.Debug($"Found version with pattern '{pattern}': {version}");
                                return version;
                            }
                        }
                    }

                    // 方法3：如果响应本身就是版本号
                    if (Regex.IsMatch(response, @"^v?\d+(\.\d+)*(\.\d+)?$"))
                    {
                        Log.Debug($"Response is version number: {response}");
                        return response;
                    }

                    // 方法4：尝试解析为XML（如果有）
                    if (response.Contains("<?xml") || response.Contains("<version>"))
                    {
                        var xmlMatch = Regex.Match(response, @"<version>([^<]+)</version>", RegexOptions.IgnoreCase);
                        if (xmlMatch.Success)
                        {
                            string version = xmlMatch.Groups[1].Value.Trim();
                            Log.Debug($"Found version in XML: {version}");
                            return version;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Error extracting version for {modId}: {ex.Message}");
                }

                return null;
            }

            /// <summary>
            /// 解析简单JSON
            /// </summary>
            private Dictionary<string, string> ParseSimpleJson(string json)
            {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                try
                {
                    // 移除空格和换行（但保留字符串内的）
                    json = Regex.Replace(json, @"\s+", " ").Trim();

                    // 移除外层大括号
                    if (json.StartsWith("{") && json.EndsWith("}"))
                    {
                        json = json.Substring(1, json.Length - 2).Trim();
                    }

                    // 分割键值对
                    var pairs = new List<string>();
                    int braceCount = 0;
                    int bracketCount = 0;
                    bool inString = false;
                    char stringChar = '\0';
                    int start = 0;

                    for (int i = 0; i < json.Length; i++)
                    {
                        char c = json[i];

                        // 处理字符串
                        if (c == '"' || c == '\'')
                        {
                            if (!inString)
                            {
                                inString = true;
                                stringChar = c;
                            }
                            else if (c == stringChar)
                            {
                                inString = false;
                            }
                        }
                        // 处理大括号和中括号
                        else if (!inString)
                        {
                            if (c == '{') braceCount++;
                            else if (c == '}') braceCount--;
                            else if (c == '[') bracketCount++;
                            else if (c == ']') bracketCount--;
                            else if (c == ',' && braceCount == 0 && bracketCount == 0)
                            {
                                pairs.Add(json.Substring(start, i - start).Trim());
                                start = i + 1;
                            }
                        }
                    }

                    // 添加最后一个
                    if (start < json.Length)
                    {
                        pairs.Add(json.Substring(start).Trim());
                    }

                    // 解析每个键值对
                    foreach (var pair in pairs)
                    {
                        var match = Regex.Match(pair, @"^([""']?)([^""':]+)\1\s*:\s*(.+)");
                        if (match.Success)
                        {
                            string key = match.Groups[2].Value.Trim();
                            string value = match.Groups[3].Value.Trim();

                            // 清理值（移除引号）
                            if (value.StartsWith("\"") && value.EndsWith("\""))
                                value = value.Substring(1, value.Length - 2);
                            else if (value.StartsWith("'") && value.EndsWith("'"))
                                value = value.Substring(1, value.Length - 2);

                            result[key] = value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Simple JSON parse error: {ex.Message}");
                }

                return result;
            }

            /// <summary>
            /// 从响应中提取额外信息
            /// </summary>
            private void ExtractAdditionalInfo(string response, ModUpdateInfo updateInfo)
            {
                try
                {
                    var jsonData = ParseSimpleJson(response);

                    // 提取下载链接
                    if (jsonData.ContainsKey("downloadUrl") && string.IsNullOrEmpty(updateInfo.UpdateDownloadUrl))
                    {
                        updateInfo.UpdateDownloadUrl = jsonData["downloadUrl"];
                    }
                    else if (jsonData.ContainsKey("download_url") && string.IsNullOrEmpty(updateInfo.UpdateDownloadUrl))
                    {
                        updateInfo.UpdateDownloadUrl = jsonData["download_url"];
                    }
                    else if (jsonData.ContainsKey("url") && string.IsNullOrEmpty(updateInfo.UpdateDownloadUrl))
                    {
                        updateInfo.UpdateDownloadUrl = jsonData["url"];
                    }

                    // 提取更新日志
                    if (jsonData.ContainsKey("changelog") && string.IsNullOrEmpty(updateInfo.Changelog))
                    {
                        updateInfo.Changelog = jsonData["changelog"];
                    }
                    else if (jsonData.ContainsKey("change_log") && string.IsNullOrEmpty(updateInfo.Changelog))
                    {
                        updateInfo.Changelog = jsonData["change_log"];
                    }

                    // 提取发布日期
                    if (jsonData.ContainsKey("releaseDate"))
                    {
                        // 可以存储但不使用
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug($"Error extracting additional info: {ex.Message}");
                }
            }

            /// <summary>
            /// 清理版本号字符串
            /// </summary>
            private string CleanVersion(string version)
            {
                if (string.IsNullOrEmpty(version))
                    return "0.0.0";

                version = version.Trim();

                // 移除 "v" 前缀
                if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    version = version.Substring(1);

                // 确保至少有两个部分
                var parts = version.Split('.');
                if (parts.Length < 2)
                    version = version + ".0";
                if (parts.Length < 3)
                    version = version + ".0";

                return version;
            }

            /// <summary>
            /// 比较版本号
            /// </summary>
            private bool IsNewerVersion(string newVersion, string currentVersion)
            {
                try
                {
                    var newParts = newVersion.Split('.').Select(p =>
                        int.TryParse(p, out int result) ? result : 0).ToArray();
                    var currentParts = currentVersion.Split('.').Select(p =>
                        int.TryParse(p, out int result) ? result : 0).ToArray();

                    // 确保数组长度相同
                    int maxLength = Math.Max(newParts.Length, currentParts.Length);
                    Array.Resize(ref newParts, maxLength);
                    Array.Resize(ref currentParts, maxLength);

                    for (int i = 0; i < maxLength; i++)
                    {
                        if (newParts[i] > currentParts[i]) return true;
                        if (newParts[i] < currentParts[i]) return false;
                    }

                    return false; // 版本相同
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>
            /// 生成下载链接
            /// </summary>
            private string GenerateDownloadUrl(string checkUrl, string version)
            {
                try
                {
                    // GitHub Raw URL 转换
                    if (checkUrl.Contains("raw.githubusercontent.com"))
                    {
                        // 从：https://raw.githubusercontent.com/User/Repo/main/version.json
                        // 到：https://github.com/User/Repo/releases/download/v1.0.0/Mod.zip

                        var uri = new Uri(checkUrl);
                        var segments = uri.AbsolutePath.Split('/');

                        if (segments.Length >= 4)
                        {
                            string user = segments[1];
                            string repo = segments[2];

                            return $"https://github.com/{user}/{repo}/releases/download/v{version}/{repo}.zip";
                        }
                    }
                    // 普通 GitHub URL
                    else if (checkUrl.Contains("github.com"))
                    {
                        var uri = new Uri(checkUrl);
                        var segments = uri.AbsolutePath.Split('/');

                        if (segments.Length >= 3)
                        {
                            string user = segments[1];
                            string repo = segments[2];

                            return $"https://github.com/{user}/{repo}/releases/download/v{version}/{repo}.zip";
                        }
                    }
                }
                catch { }

                return checkUrl; // 无法生成，返回原URL
            }
        }
    }
}
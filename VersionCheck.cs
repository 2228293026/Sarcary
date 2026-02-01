using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;

namespace Sarcary
{
    public class VersionCheck
    {
        private UnityModManager.ModEntry modEntry;
        private bool updateAvailable = false;
        private string latestVersion = "";
        private string updateUrl = "";
        private string changelog = "";
        private float lastCheckTime = 0f;
        private const float CHECK_INTERVAL = 3600f; // 1小时检查一次

        public VersionCheck(UnityModManager.ModEntry entry)
        {
            modEntry = entry;
        }

        public async void CheckForUpdates(bool force = false)
        {
            try
            {
                if (!force && Time.time - lastCheckTime < CHECK_INTERVAL) return;

                Log.Info("Checking for updates...");

                // 更新服务器URL
                string updateServerUrl = "https://gitee.com/hitmargin/update/raw/master/Sarcary.json";

                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", "Sarcary-Mod");
                    client.Encoding = Encoding.UTF8;

                    string jsonData = await client.DownloadStringTaskAsync(updateServerUrl);

                    // 解析JSON响应
                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        ParseUpdateInfo(jsonData);
                    }
                    else
                    {
                        Log.Warning("Received empty update data");
                    }
                }

                lastCheckTime = Time.time;
            }
            catch (WebException webEx)
            {
                if (webEx.Status == WebExceptionStatus.Timeout)
                {
                    Log.Warning("Update check timeout");
                }
                else
                {
                    Log.Error($"Network error checking updates: {webEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to check for updates: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析更新信息
        /// </summary>
        private void ParseUpdateInfo(string jsonData)
        {
            try
            {
                jsonData = jsonData.Trim();
                Log.Debug($"Raw update data: {jsonData}");

                string currentVersion = Main.GetCleanVersion(modEntry.Info.Version);
                Log.Info($"Current version: {currentVersion}");

                // 尝试多种方式解析
                bool parsed = false;

                // 方法1：使用JsonUtility
                if (jsonData.StartsWith("{") && jsonData.EndsWith("}"))
                {
                    try
                    {
                        var updateData = JsonUtility.FromJson<UpdateData>(jsonData);
                        if (updateData != null)
                        {
                            latestVersion = updateData.version ?? "";
                            updateUrl = updateData.url ?? "";
                            changelog = updateData.changelog ?? "";
                            parsed = true;
                            Log.Debug("Parsed using JsonUtility");
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        Log.Debug($"JsonUtility parse failed: {jsonEx.Message}");
                    }
                }

                // 方法2：使用简单JSON解析
                if (!parsed)
                {
                    var jsonDict = ParseSimpleJson(jsonData);
                    if (jsonDict.ContainsKey("version"))
                    {
                        latestVersion = jsonDict["version"];
                        if (jsonDict.ContainsKey("url")) updateUrl = jsonDict["url"];
                        if (jsonDict.ContainsKey("downloadUrl")) updateUrl = jsonDict["downloadUrl"];
                        if (jsonDict.ContainsKey("changelog")) changelog = jsonDict["changelog"];
                        parsed = true;
                        Log.Debug("Parsed using simple JSON parser");
                    }
                }

                // 方法3：使用正则表达式
                if (!parsed)
                {
                    // 查找version字段
                    var versionMatch = Regex.Match(jsonData, @"""version""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                    if (versionMatch.Success)
                    {
                        latestVersion = versionMatch.Groups[1].Value.Trim();

                        // 查找其他字段
                        var urlMatch = Regex.Match(jsonData, @"""url""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                        if (urlMatch.Success) updateUrl = urlMatch.Groups[1].Value.Trim();

                        var changelogMatch = Regex.Match(jsonData, @"""changelog""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                        if (changelogMatch.Success) changelog = changelogMatch.Groups[1].Value.Trim();

                        parsed = true;
                        Log.Debug("Parsed using regex");
                    }
                }

                if (parsed && !string.IsNullOrEmpty(latestVersion))
                {
                    Log.Info($"Latest version found: {latestVersion}");

                    // 清理版本号
                    latestVersion = CleanVersion(latestVersion);
                    currentVersion = CleanVersion(currentVersion);

                    if (IsNewerVersion(latestVersion, currentVersion))
                    {
                        updateAvailable = true;
                        Log.Info($"Update available: {latestVersion} (Current: {currentVersion})");

                        // 如果URL为空，生成默认URL
                        if (string.IsNullOrEmpty(updateUrl))
                        {
                            updateUrl = GenerateDefaultDownloadUrl(latestVersion);
                        }

                        // 显示更新通知
                        ShowUpdateNotification();
                    }
                    else
                    {
                        updateAvailable = false;
                        Log.Info("Mod is up to date.");
                    }
                }
                else
                {
                    Log.Warning("Could not parse version from update data");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error parsing update info: {ex.Message}");
            }
        }

        /// <summary>
        /// 简单JSON解析
        /// </summary>
        private Dictionary<string, string> ParseSimpleJson(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                json = json.Trim();

                // 移除外层大括号
                if (json.StartsWith("{") && json.EndsWith("}"))
                {
                    json = json.Substring(1, json.Length - 2).Trim();
                }

                // 分割键值对
                var pairs = Regex.Split(json, @",(?=(?:[^""]*""[^""]*"")*[^""]*$)")
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                foreach (var pair in pairs)
                {
                    var match = Regex.Match(pair, @"^\s*[""']?([^""':]+)[""']?\s*:\s*(.+)$");
                    if (match.Success)
                    {
                        string key = match.Groups[1].Value.Trim();
                        string value = match.Groups[2].Value.Trim();

                        // 清理值
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
                Log.Debug($"Simple JSON parse error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 清理版本号
        /// </summary>
        private string CleanVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "0.0.0";

            version = version.Trim();

            // 移除 "v" 前缀
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                version = version.Substring(1);

            // 确保格式正确
            var parts = version.Split('.');
            if (parts.Length == 1)
                version = version + ".0.0";
            else if (parts.Length == 2)
                version = version + ".0";

            return version;
        }

        private bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                Log.Debug($"Comparing versions: new={newVersion}, current={currentVersion}");

                var newParts = newVersion.Split('.').Select(p =>
                    int.TryParse(p, out int result) ? result : 0).ToArray();
                var currentParts = currentVersion.Split('.').Select(p =>
                    int.TryParse(p, out int result) ? result : 0).ToArray();

                // 确保长度相同
                int maxLength = Math.Max(newParts.Length, currentParts.Length);
                Array.Resize(ref newParts, maxLength);
                Array.Resize(ref currentParts, maxLength);

                for (int i = 0; i < maxLength; i++)
                {
                    if (newParts[i] > currentParts[i])
                    {
                        Log.Debug($"Part {i}: {newParts[i]} > {currentParts[i]} -> NEWER");
                        return true;
                    }
                    if (newParts[i] < currentParts[i])
                    {
                        Log.Debug($"Part {i}: {newParts[i]} < {currentParts[i]} -> OLDER");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"Version comparison error: {ex.Message}");
                return false;
            }
        }

        private void ShowUpdateNotification()
        {
            // 在屏幕上显示通知
            Log.Warning($"============================================");
            Log.Warning($"UPDATE AVAILABLE!");
            Log.Warning($"Current: v{Main.GetCleanVersion(modEntry.Info.Version)}");
            Log.Warning($"Latest: v{latestVersion}");
            if (!string.IsNullOrEmpty(changelog))
                Log.Warning($"Changelog: {changelog}");
            if (!string.IsNullOrEmpty(updateUrl))
                Log.Warning($"Download: {updateUrl}");
            Log.Warning($"============================================");

            // 显示游戏内通知
            ShowInGameNotification();
        }

        private void ShowInGameNotification()
        {
            try
            {
                GameObject notificationObj = new GameObject("UpdateNotification");
                var notification = notificationObj.AddComponent<UpdateNotificationUI>();
                notification.Initialize(
                    latestVersion,
                    Main.GetCleanVersion(modEntry.Info.Version),
                    changelog,
                    updateUrl
                );
            }
            catch (Exception ex)
            {
                Log.Debug($"Could not show in-game notification: {ex.Message}");
            }
        }

        public void DrawUpdateNotification()
        {
            if (updateAvailable)
            {
                GUILayout.BeginArea(new Rect(10, 50, 400, 150));
                GUILayout.BeginVertical(GUI.skin.box);

                GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.yellow }
                };

                GUIStyle textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleLeft
                };

                GUILayout.Label("UPDATE AVAILABLE!", titleStyle);
                GUILayout.Space(5);

                GUILayout.Label($"Current: v{Main.GetCleanVersion(modEntry.Info.Version)}", textStyle);
                GUILayout.Label($"Latest: v{latestVersion}", textStyle);

                if (!string.IsNullOrEmpty(changelog))
                {
                    GUILayout.Space(5);
                    GUILayout.Label("Changelog:", textStyle);

                    // 显示简短的更新日志
                    string shortChangelog = changelog.Length > 100 ?
                        changelog.Substring(0, 100) + "..." : changelog;
                    GUILayout.Label(shortChangelog, textStyle);
                }

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Download", GUILayout.Height(30)))
                {
                    if (!string.IsNullOrEmpty(updateUrl))
                    {
                        Application.OpenURL(updateUrl);
                    }
                }

                if (GUILayout.Button("Changelog", GUILayout.Height(30)))
                {
                    Log.Info($"Full changelog for v{latestVersion}:\n{changelog}");
                }

                if (GUILayout.Button("Ignore", GUILayout.Height(30)))
                {
                    updateAvailable = false;
                }

                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.EndArea();
            }
        }

        public void Update()
        {
            // 定期检查更新
            if (Time.time - lastCheckTime > CHECK_INTERVAL)
            {
                CheckForUpdates();
            }
        }

        // 手动检查更新
        public void ForceCheck()
        {
            CheckForUpdates(true);
        }

        /// <summary>
        /// 生成默认下载URL
        /// </summary>
        private string GenerateDefaultDownloadUrl(string version)
        {
            return $"https://github.com/YourUsername/Sarcary/releases/tag/v{version}";
        }

        /// <summary>
        /// 更新数据类
        /// </summary>
        [System.Serializable]
        private class UpdateData
        {
            public string version;
            public string url;
            public string downloadUrl;
            public string changelog;
        }
    }

    /// <summary>
    /// 游戏内更新通知UI
    /// </summary>
    public class UpdateNotificationUI : MonoBehaviour
    {
        private string latestVersion;
        private string currentVersion;
        private string changelog;
        private string downloadUrl;
        private float displayTime = 15f;
        private float startTime;

        public void Initialize(string latest, string current, string log, string url)
        {
            latestVersion = latest;
            currentVersion = current;
            changelog = log;
            downloadUrl = url;
            startTime = Time.time;

            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            if (Time.time - startTime > displayTime)
            {
                Destroy(gameObject);
                return;
            }

            // 计算淡出效果
            float remaining = displayTime - (Time.time - startTime);
            float alpha = Mathf.Clamp01(remaining / 3f); // 最后3秒淡出

            // 背景框
            float width = 350f;
            float height = 180f;
            Rect rect = new Rect(Screen.width - width - 20, 20, width, height);

            GUI.color = new Color(0.1f, 0.1f, 0.1f, alpha * 0.9f);
            GUI.Box(rect, "");
            GUI.color = Color.white;

            // 标题
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1, 1, 0, alpha) }
            };

            GUI.Label(new Rect(rect.x + 10, rect.y + 10, rect.width - 20, 25),
                     "Sarcary 更新可用", titleStyle);

            // 版本信息
            GUIStyle versionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(1, 1, 1, alpha) }
            };

            GUI.Label(new Rect(rect.x + 10, rect.y + 40, rect.width - 20, 20),
                     $"当前版本: v{currentVersion}", versionStyle);
            GUI.Label(new Rect(rect.x + 10, rect.y + 60, rect.width - 20, 20),
                     $"最新版本: v{latestVersion}", versionStyle);

            // 按钮
            float buttonWidth = 100f;
            float buttonHeight = 25f;
            float buttonY = rect.y + height - buttonHeight - 10f;

            GUI.color = new Color(1, 1, 1, alpha);

            if (GUI.Button(new Rect(rect.x + 10, buttonY, buttonWidth, buttonHeight), "下载更新"))
            {
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    Application.OpenURL(downloadUrl);
                }
                Destroy(gameObject);
            }

            if (GUI.Button(new Rect(rect.x + 120, buttonY, buttonWidth, buttonHeight), "查看详情"))
            {
                Log.Info($"Sarcary v{latestVersion} changelog:\n{changelog}");
            }

            if (GUI.Button(new Rect(rect.x + 230, buttonY, buttonWidth, buttonHeight), "忽略"))
            {
                Destroy(gameObject);
            }

            GUI.color = Color.white;
        }
    }
}
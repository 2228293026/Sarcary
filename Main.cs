using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;

namespace Sarcary
{
    public class Main
    {
        public static UnityModManager.ModEntry mod;
        public static Settings Settings { get; private set; }
        public static Harmony HarmonyInstance { get; private set; }
        public static bool IsEnabled { get; private set; }

        // 版本检查相关
        public static VersionCheck versionChecker;
        private static float lastUpdateCheckTime = 0f;
        private const float UPDATE_CHECK_INTERVAL = 3600f; // 1小时

        public static void Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                Main.mod = modEntry;
                Settings = Settings.Load(modEntry);

                // 初始化日志系统
                Log.Initialize(modEntry);

                // 初始化版本检查
                versionChecker = new VersionCheck(modEntry);

                // 注册事件
                modEntry.OnToggle = OnToggle;
                modEntry.OnGUI = OnGUI;
                modEntry.OnSaveGUI = Settings.OnSaveGUI;
                modEntry.OnHideGUI = Settings.OnHideGUI;
                modEntry.OnUpdate = OnUpdate;
                modEntry.OnFixedGUI = OnFixedGUI;

                Log.Info($"Sarcary Mod loaded successfully. Version: {modEntry.Info.Version}");

                // 订阅更新事件
                API.OnUpdateAvailable += OnUpdateAvailable;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load Sarcary Mod: {ex}");
                throw;
            }
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool isToggled)
        {
            try
            {
                if (isToggled)
                {
                    modEntry.Info.Version = "<color=cyan>" + modEntry.Info.Version + "</color>";
                    // 版本安全检查
                    if (!VersionSafetyCheck(modEntry))
                    {
                        return false;
                    }

                    // 创建Harmony实例
                    HarmonyInstance = new Harmony(modEntry.Info.Id);

                    // 应用所有补丁
                    HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());

                    IsEnabled = true;

                    // 检查更新
                    versionChecker?.CheckForUpdates();

                    Log.Info($"Sarcary Mod enabled. Applied {HarmonyInstance.GetPatchedMethods().Count()} patches.");
                }
                else
                {
                    // 移除所有补丁
                    HarmonyInstance?.UnpatchAll(modEntry.Info.Id);

                    IsEnabled = false;
                    Log.Info("Sarcary Mod disabled. All patches removed.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Error during toggle: {ex}");
                return false;
            }
        }

        private static bool VersionSafetyCheck(UnityModManager.ModEntry modEntry)
        {
            try
            {
                string cleanVersion = GetCleanVersion(modEntry.Info.Version);

                // 检查关键信息是否被篡改
                if (cleanVersion != "1.0.0" ||
                    modEntry.Info.Id != "Sarcary" ||
                    modEntry.Info.DisplayName != "Sarcary" ||
                    modEntry.Info.Author != "HitMargin" ||
                    modEntry.Info.AssemblyName != "Sarcary.dll" ||
                    modEntry.Info.EntryMethod != "Sarcary.Main.Load")
                {
                    Log.Error("Modifying the Info.json file is NOT allowed!");
                    modEntry.Logger.Error("Security check failed! The mod has been tampered with.");

                    // 显示错误弹窗
                    Application.Quit();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Version safety check failed: {ex}");
                return false;
            }
        }

        public static string GetCleanVersion(string version)
        {
            if (version.Contains("<color="))
            {
                int startIndex = version.IndexOf('>') + 1;
                int endIndex = version.LastIndexOf('<');
                if (startIndex > 0 && endIndex > startIndex)
                {
                    return version.Substring(startIndex, endIndex - startIndex);
                }
            }
            return version;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.OnGUI(modEntry);
        }

        private static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            // 定期检查更新
            if (Settings.autoCheckUpdates &&
                Time.time - lastUpdateCheckTime > UPDATE_CHECK_INTERVAL)
            {
                API.CheckAllUpdates();
                lastUpdateCheckTime = Time.time;
            }

            versionChecker?.Update();
        }

        private static void OnFixedGUI(UnityModManager.ModEntry modEntry)
        {
            // 在GUI上显示更新通知
            versionChecker?.DrawUpdateNotification();

            // 显示API的更新通知
            //API.DrawUpdateNotifications();
        }

        private static void OnUpdateAvailable(string modId, API.ModUpdateInfo updateInfo)
        {
            if (!Settings.showUpdateNotifications)
                return;

            string message = $"<color=#FFFF00>更新可用！</color>\n" +
                            $"{modId} v{updateInfo.CurrentVersion} → v{updateInfo.LatestVersion}\n" +
                            $"{updateInfo.Changelog}";

            Log.Warning(message);

            // 可以在游戏中显示通知
            if (Settings.enableInGameNotifications)
            {
                ShowUpdateNotification(modId, updateInfo);
            }
        }

        private static void ShowUpdateNotification(string modId, API.ModUpdateInfo updateInfo)
        {
            try
            {
                // 创建游戏内通知
                var notification = new GameObject($"UpdateNotification_{modId}");
                var component = notification.AddComponent<UpdateNotification>();
                component.Initialize(modId, updateInfo);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to show update notification: {ex.Message}");
            }
        }

        // API方法 - 供其他mod调用
        public static void RegisterExternalMod(string modId, Action<string> callback)
        {
            Log.Info($"External mod registered: {modId}");
            // 这里可以添加更多集成逻辑
        }

        public static string GetModVersion()
        {
            return GetCleanVersion(mod.Info.Version);
        }

        public static bool IsModActive()
        {
            return IsEnabled;
        }
        public static bool CheckRequiredMod(string needMod)
        {
            bool flag;
            try
            {
                string text = needMod;
                List<UnityModManager.ModEntry> modEntries = UnityModManager.modEntries;
                if (modEntries != null)
                {
                    foreach (UnityModManager.ModEntry modEntry in modEntries)
                    {
                        if (modEntry.Info.Id == text && modEntry.Enabled)
                        {
                            Log.Info(string.Concat(new string[]
                            {
                                "Found required mod: ",
                                modEntry.Info.DisplayName,
                                " Id: ",
                                modEntry.Info.Id,
                                " (Version: ",
                                modEntry.Info.Version,
                                ")"
                            }));
                            return true;
                        }
                    }
                }
                Log.Warning("Required mod not found or not enabled: " + text);
                flag = false;
            }
            catch (Exception ex)
            {
                Log.Error("Error checking required mod: " + ex.Message);
                flag = false;
            }
            return flag;
        }

    }
}
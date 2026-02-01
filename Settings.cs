using System;
using System.IO;
using UnityEngine;
using UnityModManagerNet;

namespace Sarcary
{
    public class Settings : UnityModManager.ModSettings, IDrawable
    {
        // 更新设置
        [Header("更新设置")]
        [Draw("自动检查更新")]
        public bool autoCheckUpdates = true;

        [Draw("显示更新通知")]
        public bool showUpdateNotifications = true;

        [Draw("游戏内通知")]
        public bool enableInGameNotifications = true;

        // API设置
        [Header("API设置")]
        [Draw("启用API")]
        public bool enableAPI = true;

        [Draw("允许远程更新检查")]
        public bool allowRemoteUpdateChecks = true;

        // 日志级别设置
        [Draw("日志级别")]
        public string logLevel = "Info";

        // 其他设置
        [Header("其他设置")]

        [Draw("自动保存设置")]
        public bool autoSaveSettings = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        public void OnChange()
        {
            // 应用日志级别设置
            try
            {
                if (Enum.TryParse(logLevel, out LogLevel level))
                {
                    Log.SetLogLevel(level);
                }
            }
            catch { }

            // 自动保存设置
            if (autoSaveSettings && Main.mod != null)
            {
                Save(Main.mod);
            }
        }

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            DrawAllFields();
        }

        private void DrawAllFields()
        {
            // 更新设置
            GUILayout.Label("<color=#FFFF00>更新设置</color>", new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            });

            //autoCheckUpdates = DrawToggle("自动检查更新", autoCheckUpdates);
            //showUpdateNotifications = DrawToggle("显示更新通知", showUpdateNotifications);
            enableInGameNotifications = DrawToggle("游戏内通知", enableInGameNotifications);

            GUILayout.Space(10);

            // API设置
            GUILayout.Label("<color=#FFFF00>API设置</color>", new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            });

            enableAPI = DrawToggle("启用API", enableAPI);
            GUILayout.Space(10);

            // 其他设置
            GUILayout.Label("<color=#FFFF00>其他设置</color>", new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            });

            autoSaveSettings = DrawToggle("自动保存设置", autoSaveSettings);

            // 日志级别选择
            GUILayout.BeginHorizontal();
            GUILayout.Label("日志级别:", GUILayout.Width(150));
            if (GUILayout.Toggle(logLevel == "Debug", "调试")) logLevel = "Debug";
            if (GUILayout.Toggle(logLevel == "Info", "信息")) logLevel = "Info";
            if (GUILayout.Toggle(logLevel == "Warning", "警告")) logLevel = "Warning";
            if (GUILayout.Toggle(logLevel == "Error", "错误")) logLevel = "Error";
            GUILayout.EndHorizontal();
        }

        private bool DrawToggle(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(150));
            bool newValue = GUILayout.Toggle(value, "");
            GUILayout.EndHorizontal();
            return newValue;
        }
        public void OnHideGUI(UnityModManager.ModEntry modEntry)
        {
            Save(modEntry);
        }

        public void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Save(modEntry);
        }

        public static Settings Load(UnityModManager.ModEntry modEntry)
        {
            return Load<Settings>(modEntry);
        }
    }
}
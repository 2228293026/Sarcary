using System.Collections.Generic;
using UnityEngine;

namespace Sarcary
{
    /// <summary>
    /// 游戏内更新通知组件
    /// </summary>
    public class UpdateNotification : MonoBehaviour
    {
        private string modId;
        private API.ModUpdateInfo updateInfo;
        private float yPosition = 10f; // 垂直位置
        private static float currentYPosition = 10f; // 静态变量跟踪当前位置
        private static List<UpdateNotification> activeNotifications = new List<UpdateNotification>();
        private bool isActive = false;

        public void Initialize(string modId, API.ModUpdateInfo updateInfo)
        {
            this.modId = modId;
            this.updateInfo = updateInfo;

            // 设置垂直位置
            yPosition = currentYPosition;
            currentYPosition += 70f; // 每个通知高70像素，加上间距

            isActive = true;
            activeNotifications.Add(this);

            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (isActive)
            {
                activeNotifications.Remove(this);
                isActive = false;
                RearrangeNotifications();
            }
        }

        void RearrangeNotifications()
        {
            // 重新排列剩余的通知
            currentYPosition = 10f;
            foreach (var notification in activeNotifications)
            {
                notification.yPosition = currentYPosition;
                currentYPosition += 70f;
            }
        }

        void DestroyNotification()
        {
            if (isActive)
            {
                isActive = false;
                activeNotifications.Remove(this);
                Destroy(gameObject);
                RearrangeNotifications();
            }
        }

        void OnGUI()
        {
            if (!isActive) return;

            // 计算透明度（最后3秒淡出）

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 14;
            style.normal.textColor = new Color(1, 1, 0, 1);
            style.alignment = TextAnchor.UpperLeft;

            string message = $"<color=#FFFF00>●</color> {modId} 有更新可用!\n" +
                            $"v{updateInfo.CurrentVersion} → v{updateInfo.LatestVersion}";

            Rect rect = new Rect(Screen.width - 320, yPosition, 310, 60);

            // 背景
            GUI.color = new Color(0.1f, 0.1f, 0.1f, 1 * 0.8f);
            GUI.Box(rect, "");
            GUI.color = Color.white;

            // 文本
            GUI.Label(new Rect(rect.x + 5, rect.y + 5, rect.width - 10, 25),
                     $"<color=#FFFF00>●</color> {modId} 更新可用", style);

            GUIStyle versionStyle = new GUIStyle(GUI.skin.label);
            versionStyle.normal.textColor = new Color(1, 1, 1, 1);
            versionStyle.fontSize = 12;

            GUI.Label(new Rect(rect.x + 5, rect.y + 30, rect.width - 10, 20),
                     $"v{updateInfo.CurrentVersion} → v{updateInfo.LatestVersion}", versionStyle);

            GUI.color = new Color(1, 1, 1, 1);

            if (GUI.Button(new Rect(rect.x + 10, rect.y + 50, 90, 20), "下载更新"))
            {
                API.OpenUpdateDownload(modId);
                DestroyNotification();
            }

            if (GUI.Button(new Rect(rect.x + 110, rect.y + 50, 110, 20), "查看详情(日志)"))
            {
                Log.Info($"更新详情 {modId}: {updateInfo.Changelog}");
            }

            if (GUI.Button(new Rect(rect.x + 230, rect.y + 50, 90, 20), "忽略"))
            {
                DestroyNotification();
            }

            GUI.color = Color.white;
        }

        /// <summary>
        /// 清除所有通知
        /// </summary>
        public static void ClearAllNotifications()
        {
            foreach (var notification in activeNotifications.ToArray())
            {
                notification.DestroyNotification();
            }
            activeNotifications.Clear();
            currentYPosition = 10f;
        }

        /// <summary>
        /// 获取活动通知数量
        /// </summary>
        public static int GetActiveNotificationCount()
        {
            return activeNotifications.Count;
        }
    }
}
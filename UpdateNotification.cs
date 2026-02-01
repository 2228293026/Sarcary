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

        public void Initialize(string modId, API.ModUpdateInfo updateInfo)
        {
            this.modId = modId;
            this.updateInfo = updateInfo;

            DontDestroyOnLoad(gameObject);
        }

        private void OnGUI()
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 14;
            //style.normal.textColor = new Color(1, 1, 0, alpha);
            style.alignment = TextAnchor.UpperLeft;

            string message = $"<color=#FFFF00>●</color> {modId} 有更新可用!\n" +
                            $"v{updateInfo.CurrentVersion} → v{updateInfo.LatestVersion}";

            Rect rect = new Rect(Screen.width - 320, 10, 310, 60);
            GUI.Box(rect, message, style);

            if (GUI.Button(new Rect(rect.x + 10, rect.y + 35, 90, 20), "下载更新"))
            {
                API.OpenUpdateDownload(modId);
                Destroy(gameObject);
            }

            if (GUI.Button(new Rect(rect.x + 110, rect.y + 35, 90, 20), "查看详情(日志)"))
            {
                Log.Info($"更新详情 {modId}: {updateInfo.Changelog}");
            }

            if (GUI.Button(new Rect(rect.x + 210, rect.y + 35, 90, 20), "忽略"))
            {
                Destroy(gameObject);
            }
        }
    }
}
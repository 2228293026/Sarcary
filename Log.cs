using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace Sarcary
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4,
        Patch = 5 // 专门用于补丁日志
    }

    public static class Log
    {
        private static UnityModManager.ModEntry modEntry;
        private static string logFilePath;
        private static LogLevel minimumLogLevel = LogLevel.Info;
        private static bool enableUnityConsole = true;

        // 日志颜色配置
        private static readonly Dictionary<LogLevel, string> logColors = new Dictionary<LogLevel, string>
        {
            { LogLevel.Debug, "<color=#888888>" },
            { LogLevel.Info, "<color=#FFFFFF>" },
            { LogLevel.Warning, "<color=#FFFF00>" },
            { LogLevel.Error, "<color=#FF0000>" },
            { LogLevel.Critical, "<color=#FF00FF>" },
            { LogLevel.Patch, "<color=#00FFFF>" }
        };

        public static void Initialize(UnityModManager.ModEntry entry)
        {
            modEntry = entry;

            // 设置日志文件路径
            logFilePath = Path.Combine(modEntry.Path, "Log.txt");

            // 清理旧的日志文件（保留最近3个）
            CleanOldLogs();

            // 写入初始日志
            FileLog("========================================", LogLevel.Info, false);
            FileLog($"Mod Log - {DateTime.Now}", LogLevel.Info, false);
            FileLog($"Version: {modEntry.Info.Version}", LogLevel.Info, false);
            FileLog("========================================", LogLevel.Info, false);

            Debug($"Log system initialized. Minimum log level: {minimumLogLevel}");
        }

        public static void SetLogLevel(LogLevel level)
        {
            minimumLogLevel = level;
            Info($"Log level changed to: {level}");
        }

        public static void EnableUnityConsoleLogging(bool enable)
        {
            enableUnityConsole = enable;
            Info($"Unity console logging {(enable ? "enabled" : "disabled")}");
        }

        // 基础日志方法
        private static void LogMessage(string message, LogLevel level, bool showStackTrace = false)
        {
            if (level < minimumLogLevel) return;

            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
            string coloredMessage = $"{logColors[level]}{formattedMessage}</color>";

            // 输出到UnityModManager
            if (modEntry != null)
            {
                switch (level)
                {
                    case LogLevel.Debug:
                    case LogLevel.Info:
                    case LogLevel.Patch:
                        modEntry.Logger.Log(formattedMessage);
                        break;
                    case LogLevel.Warning:
                        modEntry.Logger.Warning(formattedMessage);
                        break;
                    case LogLevel.Error:
                    case LogLevel.Critical:
                        modEntry.Logger.Error(formattedMessage);
                        break;
                }
            }

            // 输出到Unity控制台
            if (enableUnityConsole)
            {
                switch (level)
                {
                    case LogLevel.Debug:
                        UnityEngine.Debug.Log(formattedMessage);
                        break;
                    case LogLevel.Info:
                    case LogLevel.Patch:
                        UnityEngine.Debug.Log(coloredMessage);
                        break;
                    case LogLevel.Warning:
                        UnityEngine.Debug.LogWarning(coloredMessage);
                        break;
                    case LogLevel.Error:
                    case LogLevel.Critical:
                        UnityEngine.Debug.LogError(coloredMessage);
                        break;
                }
            }

            // 写入文件
            if (Main.Settings.exportLocalLogs)
            {
                FileLog(formattedMessage, level, showStackTrace);
            }
        }

        // 文件日志记录
        private static void FileLog(string message, LogLevel level, bool includeStackTrace)
        {
            try
            {
                string logEntry = message;

                if (includeStackTrace && level >= LogLevel.Error)
                {
                    logEntry += $"\nStack Trace:\n{new StackTrace(true)}";
                }

                File.AppendAllText(logFilePath, logEntry + "\n", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to write to log file: {ex.Message}");
            }
        }

        // 清理旧的日志文件
        private static void CleanOldLogs()
        {
            try
            {
                string directory = Path.GetDirectoryName(logFilePath);
                if (!Directory.Exists(directory)) return;

                var logFiles = Directory.GetFiles(directory, "Log*.txt")
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .ToList();

                // 保留最近的3个日志文件
                for (int i = 3; i < logFiles.Count; i++)
                {
                    File.Delete(logFiles[i]);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Failed to clean old logs: {ex.Message}");
            }
        }

        // 公开的日志方法
        public static void Debug(string message) => LogMessage(message, LogLevel.Debug);
        public static void Info(string message) => LogMessage(message, LogLevel.Info);
        public static void Warning(string message) => LogMessage(message, LogLevel.Warning);
        public static void Error(string message) => LogMessage(message, LogLevel.Error, true);
        public static void Critical(string message) => LogMessage(message, LogLevel.Critical, true);
        public static void Patch(string message) => LogMessage($"[PATCH] {message}", LogLevel.Patch);

        // Harmony补丁结果日志
        public static void PatchResult(string methodName, bool success, string details = "")
        {
            string result = success ? "SUCCESS" : "FAILED";
            string message = $"Patch {methodName}: {result}";
            if (!string.IsNullOrEmpty(details))
                message += $" - {details}";

            Patch(message);
        }

        // 获取日志文件路径
        public static string GetLogFilePath() => logFilePath;

        // 清空日志文件
        public static void ClearLogFile()
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    File.WriteAllText(logFilePath, "");
                    Info("Log file cleared.");
                }
            }
            catch (Exception ex)
            {
                Error($"Failed to clear log file: {ex}");
            }
        }

        // 性能监控
        public static void MeasurePerformance(string operationName, Action action)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                action?.Invoke();
            }
            finally
            {
                stopwatch.Stop();
                Debug($"Performance: {operationName} took {stopwatch.ElapsedMilliseconds}ms");
            }
        }
    }
}
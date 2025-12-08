using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WommeAPI.Helpers
{
    public static class SyncLogger
    {
        private static readonly string logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");

        static SyncLogger()
        {
            Directory.CreateDirectory(logDirectory);
        }

        // Method to log key-value pairs (e.g., job=J001)
        public static void Log(string tableName, Dictionary<string, object> primaryKeys)
        {
            var logFilePath = Path.Combine(logDirectory, $"{tableName}_SyncLog.txt");
            var sb = new StringBuilder();

            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Table: {tableName}");
            foreach (var kvp in primaryKeys)
            {
                sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();

            File.AppendAllText(logFilePath, sb.ToString());
        }

        // Method to log plain messages (e.g., "Sync started")
        public static void Log(string tableName, string message)
        {
            var logFilePath = Path.Combine(logDirectory, $"{tableName}_SyncLog.txt");
            var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logFilePath, logEntry);
        }
    }
}

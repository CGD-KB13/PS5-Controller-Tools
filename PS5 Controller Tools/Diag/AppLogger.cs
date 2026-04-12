using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace PS5_Controller_Tools
{
    internal static class AppLogger
    {
        private const int RetentionDays = 14;

        private static readonly object SyncRoot = new();
        private static readonly string LogDirectoryPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PS5_Controller_Tools",
                "Logs");

        private static readonly string LogFilePath =
            Path.Combine(
                LogDirectoryPath,
                $"app-{DateTime.Now:yyyy-MM-dd}.log");

        private static bool _sessionHeaderWritten;
        private static bool _retentionApplied;

        public static void Info(string source, string message)
        {
            Write("INFO", source, message, null);
        }

        public static void Warn(string source, string message)
        {
            Write("WARN", source, message, null);
        }

        public static void Error(string source, string message, Exception? exception = null)
        {
            Write("ERROR", source, message, exception);
        }

        public static string GetLogFilePath()
        {
            return LogFilePath;
        }

        public static string GetLogDirectoryPath()
        {
            return LogDirectoryPath;
        }

        private static void Write(string level, string source, string message, Exception? exception)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            string line = $"[{timestamp}] [{level}] [{source}] {message}";

            Debug.WriteLine(line);
            Trace.WriteLine(line);

            if (exception != null)
            {
                Debug.WriteLine(exception);
                Trace.WriteLine(exception);
            }

            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(LogDirectoryPath);

                    if (!_retentionApplied)
                    {
                        ApplyRetentionPolicy();
                        _retentionApplied = true;
                    }

                    using var writer = new StreamWriter(LogFilePath, append: true, Encoding.UTF8);

                    if (!_sessionHeaderWritten)
                    {
                        writer.WriteLine("============================================================");
                        writer.WriteLine($"Session start : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                        writer.WriteLine($"Machine       : {Environment.MachineName}");
                        writer.WriteLine($"OS            : {Environment.OSVersion}");
                        writer.WriteLine($".NET          : {Environment.Version}");
                        writer.WriteLine($"Logs folder   : {LogDirectoryPath}");
                        writer.WriteLine("============================================================");
                        _sessionHeaderWritten = true;
                    }

                    writer.WriteLine(line);

                    if (exception != null)
                    {
                        writer.WriteLine(exception);
                    }
                }
            }
            catch
            {
                // On ne relance jamais une erreur du logger.
            }
        }

        private static void ApplyRetentionPolicy()
        {
            if (!Directory.Exists(LogDirectoryPath))
                return;

            DateTime threshold = DateTime.Now.Date.AddDays(-RetentionDays);

            foreach (string filePath in Directory.GetFiles(LogDirectoryPath, "app-*.log"))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);

                    if (fileInfo.LastWriteTime < threshold)
                    {
                        fileInfo.Delete();
                    }
                }
                catch
                {
                    // On ignore toute erreur de purge pour ne jamais perturber l'application.
                }
            }
        }
    }
}

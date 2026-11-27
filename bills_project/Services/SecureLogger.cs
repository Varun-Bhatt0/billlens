using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using bills_project.App_Config;

namespace bills_project.Services
{
    // Structured (JSON Lines) logger.
    //   App_Data/logs/app-YYYYMMDD.log       every event
    //   App_Data/logs/security-YYYYMMDD.log  security events only (logins, lockouts, CSRF/rate-limit blocks, access denied)
    // Each line carries the request ID, user, client IP and path (supplied by Global.asax via ContextProvider),
    // so one request can be followed across log lines and matched to the reference shown on the error page.
    // App_Data is never served by IIS, so log files can't be downloaded from the site.
    public static class SecureLogger
    {
        public enum Level { Debug = 1, Info = 2, Warning = 3, Error = 4, Critical = 5 }

        private const long MaxFileBytes = 10L * 1024 * 1024; // roll over at 10 MB
        private const int KeepDays = 30;
        private static readonly object Sync = new object();
        private static DateTime _lastCleanupDate = DateTime.MinValue;

        // Set by Global.asax: returns requestId/user/ip/path for the current request (or null outside a request).
        public static Func<IDictionary<string, object>> ContextProvider { get; set; }

        private static string LogDir => AppSettings.MapPath(AppSettings.LogDirectory);

        private static Level MinimumLevel
        {
            get
            {
                string configured = AppSettings.LogLevel;
                if (string.Equals(configured, "WARN", StringComparison.OrdinalIgnoreCase)) return Level.Warning;
                return Enum.TryParse(configured, true, out Level lvl) ? lvl : Level.Info;
            }
        }

        public static void LogDebug(string eventName, string message, object properties = null)
            => Write(Level.Debug, eventName, message, properties, null, security: false);

        public static void LogInfo(string eventName, string message, object properties = null)
            => Write(Level.Info, eventName, message, properties, null, security: false);

        public static void LogWarning(string eventName, string message, object properties = null)
            => Write(Level.Warning, eventName, message, properties, null, security: false);

        public static void LogError(string eventName, string message, Exception exception = null, object properties = null)
            => Write(Level.Error, eventName, message, properties, exception, security: false);

        public static void LogCritical(string eventName, string message, Exception exception = null, object properties = null)
            => Write(Level.Critical, eventName, message, properties, exception, security: false);

        // Security-relevant event: written to both the app log and the security log, regardless of LOG_LEVEL.
        public static void Security(string eventName, string message, object properties = null, Level level = Level.Warning)
            => Write(level, eventName, message, properties, null, security: true);

        private static void Write(Level level, string eventName, string message, object properties, Exception exception, bool security)
        {
            if (!security && level < MinimumLevel) return;
            try
            {
                IDictionary<string, object> context = null;
                try { context = ContextProvider?.Invoke(); } catch { /* no request context */ }

                string line = LogFormatter.Format(DateTime.UtcNow, level.ToString().ToUpperInvariant(), eventName, message, context, properties, exception);
                string day = DateTime.UtcNow.ToString("yyyyMMdd");

                lock (Sync)
                {
                    Directory.CreateDirectory(LogDir);
                    CleanupOldFiles();
                    Append(Path.Combine(LogDir, $"app-{day}.log"), line);
                    if (security)
                        Append(Path.Combine(LogDir, $"security-{day}.log"), line);
                }
            }
            catch (Exception ex)
            {
                // Logging must never break a request.
                Trace.WriteLine($"[LOG_FAIL] {eventName}: {message} ({ex.Message})");
            }
        }

        private static void Append(string path, string line)
        {
            if (File.Exists(path) && new FileInfo(path).Length > MaxFileBytes)
            {
                string rolled = path + "." + DateTime.UtcNow.ToString("HHmmss");
                File.Move(path, rolled);
            }
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
        }

        // Deletes log files older than KeepDays, at most once a day.
        private static void CleanupOldFiles()
        {
            if (_lastCleanupDate == DateTime.UtcNow.Date) return;
            _lastCleanupDate = DateTime.UtcNow.Date;
            DateTime cutoff = DateTime.UtcNow.AddDays(-KeepDays);
            foreach (var file in Directory.GetFiles(LogDir, "*.log*").Where(f => File.GetLastWriteTimeUtc(f) < cutoff))
            {
                try { File.Delete(file); } catch (Exception ex) { Trace.WriteLine($"[LOG_CLEANUP_FAIL] {file}: {ex.Message}"); }
            }
        }
    }
}

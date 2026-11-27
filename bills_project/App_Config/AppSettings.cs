using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using bills_project.Services;

namespace bills_project.App_Config
{
    // The single place the app reads Web.config <appSettings> (and App_Data\secrets.config, which Web.config merges in).
    public static class AppSettings
    {
        // ---- Database ----
        public static string OracleUser => Get("ORACLE_USER");
        public static string OraclePassword => Get("ORACLE_PASSWORD");
        public static string OracleDSN => FixOracleDsn(Get("ORACLE_DSN", "localhost:1521/FREEPDB1"));
        public static bool OracleConfigured => !string.IsNullOrWhiteSpace(OracleUser) && !string.IsNullOrWhiteSpace(OraclePassword);
        public static bool UseSqliteFallback => GetBool("USE_SQLITE_FALLBACK", true);
        public static string SqliteDatabase => Get("SQLITE_DATABASE", "App_Data/bills.db");
        // Creates two demo logins in an empty SQLite database. Turned off in Web.Release.config.
        public static bool SeedDemoUsers => GetBool("SEED_DEMO_USERS", false);

        // ---- Uploads ----
        public static string UploadFolder => Get("UPLOAD_FOLDER", "App_Data/Bills");
        public static int MaxContentLengthMB => GetInt("MAX_CONTENT_LENGTH_MB", 5);
        public static IReadOnlyList<string> AllowedExtensions =>
            Get("ALLOWED_EXTENSIONS", ".pdf,.jpg,.jpeg,.png")
                .Split(',').Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).ToList();

        // ---- OCR ----
        public static int OcrMaxImageSize => GetInt("OCR_MAX_IMAGE_SIZE", 4096);
        public static int OcrTimeoutSeconds => GetInt("OCR_TIMEOUT", 30);
        public static string TesseractPath => DetectPath("TESSERACT_PATH", new[] { @"C:\Program Files\Tesseract-OCR\tesseract.exe", "/usr/bin/tesseract" });
        public static string PopplerPath => DetectPath("POPPLER_PATH", new[] { @"C:\Program Files\poppler\Library\bin", "/usr/bin" });

        // ---- Logging ----
        public static string LogLevel => Get("LOG_LEVEL", "INFO");
        public static string LogDirectory => Get("LOG_DIRECTORY", "App_Data/logs");

        // ---- Sessions and rate limiting ----
        public static int SessionLifetimeSeconds => GetInt("PERMANENT_SESSION_LIFETIME", 1800);

        // RATE_LIMITS="general:60 per minute,login:10 per minute,upload:10 per minute,save:20 per minute"
        public static IReadOnlyDictionary<string, RateLimitPolicy> RateLimits => _rateLimits.Value;
        private static readonly Lazy<Dictionary<string, RateLimitPolicy>> _rateLimits = new Lazy<Dictionary<string, RateLimitPolicy>>(() =>
        {
            var result = new Dictionary<string, RateLimitPolicy>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in ParsePairs(Get("RATE_LIMITS", "")))
            {
                if (RateLimitPolicy.TryParse(pair.Value, out var policy))
                    result[pair.Key] = policy;
            }
            return result;
        });

        public static RateLimitPolicy RateLimit(string name, RateLimitPolicy fallback)
            => RateLimits.TryGetValue(name, out var p) ? p : fallback;

        // LOGIN_LOCKOUT="5 per 15 minutes": failed logins allowed per account before it is locked for the rest of the window
        public static RateLimitPolicy LoginLockout =>
            RateLimitPolicy.TryParse(Get("LOGIN_LOCKOUT", ""), out var p) ? p : new RateLimitPolicy(5, TimeSpan.FromMinutes(15));

        // ---- User-facing messages ----
        public static IReadOnlyDictionary<string, string> ErrorMessages => _errorMessages.Value;
        private static readonly Lazy<Dictionary<string, string>> _errorMessages = new Lazy<Dictionary<string, string>>(
            () => ParsePairs(Get("ERROR_MESSAGES", "")));

        public static string Message(string key, string fallback)
            => ErrorMessages.TryGetValue(key, out var m) ? m : fallback;

        // ---- Helpers ----
        private static string Get(string key, string fallback = null)
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static int GetInt(string key, int fallback)
            => int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        private static bool GetBool(string key, bool fallback)
            => bool.TryParse(Get(key), out bool v) ? v : fallback;

        // "KEY:value,KEY2:value2" -> dictionary (values may contain ':' but not ',')
        private static Dictionary<string, string> ParsePairs(string raw)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw)) return dict;
            foreach (var pair in raw.Split(','))
            {
                int idx = pair.IndexOf(':');
                if (idx > 0)
                    dict[pair.Substring(0, idx).Trim()] = pair.Substring(idx + 1).Trim();
            }
            return dict;
        }

        private static string FixOracleDsn(string dsn)
        {
            if (dsn.Contains("/") && dsn.Contains(":")) return dsn;
            if (!dsn.Contains("/") && dsn.Contains(":")) return dsn + "/FREEPDB1";
            if (!dsn.Contains(":") && !dsn.Contains("/")) return "localhost:1521/" + dsn;
            return dsn;
        }

        private static string DetectPath(string key, string[] fallbacks)
        {
            var configured = Get(key);
            if (!string.IsNullOrEmpty(configured) && (File.Exists(configured) || Directory.Exists(configured)))
                return configured;
            return fallbacks.FirstOrDefault(p => File.Exists(p) || Directory.Exists(p));
        }

        // Resolves a path from config relative to the site root.
        public static string MapPath(string configuredPath)
        {
            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configuredPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

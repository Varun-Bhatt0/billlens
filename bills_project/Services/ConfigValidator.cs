using System;
using System.IO;
using bills_project.App_Config;

namespace bills_project.Services
{
    // Runs once at startup (Global.asax) and reports configuration problems early.
    public static class ConfigValidator
    {
        public static void Validate()
        {
            Directory.CreateDirectory(AppSettings.MapPath(AppSettings.UploadFolder));

            if (!AppSettings.OracleConfigured)
            {
                if (!AppSettings.UseSqliteFallback)
                    throw new InvalidOperationException("Oracle credentials are not configured (App_Data\\secrets.config) and USE_SQLITE_FALLBACK is false.");
                SecureLogger.LogWarning("config.oracle_missing", "Oracle credentials not configured; using the SQLite database");
            }

            if (AppSettings.MaxContentLengthMB < 1 || AppSettings.MaxContentLengthMB > 5)
                SecureLogger.LogWarning("config.upload_limit", "MAX_CONTENT_LENGTH_MB should be 1-5; httpRuntime maxRequestLength is 6 MB",
                    new { configured = AppSettings.MaxContentLengthMB });

            if (AppSettings.TesseractPath == null)
                SecureLogger.LogWarning("config.tesseract_missing", "Tesseract not found; OCR will not work until TESSERACT_PATH is set");

            if (AppSettings.PopplerPath == null)
                SecureLogger.LogWarning("config.poppler_missing", "Poppler not found; PDF uploads can't be read until POPPLER_PATH is set");

            if (AppSettings.SeedDemoUsers)
                SecureLogger.Security("config.demo_users_enabled", "SEED_DEMO_USERS is on: demo logins will be created in an empty SQLite database");
        }
    }
}

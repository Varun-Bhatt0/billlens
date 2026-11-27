using System;
using System.Diagnostics;
using System.Text;
using System.Web;
using bills_project.App_Config;
using bills_project.Services;

namespace bills_project.Infrastructure
{
    // GET /health: a JSON status for load balancers and uptime monitors.
    //   200 {"status":"healthy",  "database":"Oracle|SQLite","ocr":"ready", ...}
    //   503 {"status":"unhealthy","database":"unavailable", ...}
    // Deliberately reveals no versions, paths or error messages.
    public sealed class HealthCheckHandler : IHttpHandler
    {
        public bool IsReusable => true;

        public void ProcessRequest(HttpContext context)
        {
            var timer = Stopwatch.StartNew();
            string database;
            try
            {
                using (var db = new DatabaseManager())
                {
                    db.GetBillTypes(); // a real query, not just an open connection
                    database = db.Provider;
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("health.database_failed", "Health check could not reach a database", ex);
                database = "unavailable";
            }

            bool ocrReady = AppSettings.TesseractPath != null;
            bool healthy = database != "unavailable";

            var json = new StringBuilder("{");
            Field(json, "status", healthy ? (ocrReady ? "healthy" : "degraded") : "unhealthy"); json.Append(',');
            Field(json, "database", database); json.Append(',');
            Field(json, "ocr", ocrReady ? "ready" : "missing"); json.Append(',');
            json.Append("\"checkMs\":").Append(timer.ElapsedMilliseconds).Append('}');

            var response = context.Response;
            response.StatusCode = healthy ? 200 : 503;
            response.TrySkipIisCustomErrors = true;
            response.ContentType = "application/json";
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Write(json.ToString());
        }

        private static void Field(StringBuilder sb, string name, string value)
        {
            LogFormatter.AppendString(sb, name);
            sb.Append(':');
            LogFormatter.AppendString(sb, value);
        }
    }
}

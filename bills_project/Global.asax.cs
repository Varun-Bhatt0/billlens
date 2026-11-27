using System;
using System.IO;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using bills_project.App_Config;
using bills_project.Infrastructure;
using bills_project.Services;

namespace bills_project
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            SecureLogger.ContextProvider = RequestInfo.LogContext;
            ConfigValidator.Validate();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            SecureLogger.LogInfo("app.started", "Application started", new { oracleConfigured = AppSettings.OracleConfigured });
        }

        void Application_BeginRequest(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            string requestId = RequestInfo.RequestId(context);
            context.Response.AppendHeader(RequestInfo.RequestIdHeader, requestId);

            // General per-IP limit for page requests (static files and .axd script handlers aren't counted).
            string ext = Path.GetExtension(context.Request.Path);
            if (ext.Length == 0 || ext.Equals(".aspx", StringComparison.OrdinalIgnoreCase))
            {
                if (!RateLimits.Allow("general", RequestInfo.ClientIp(context), RateLimits.General, out var retryAfter))
                    RateLimits.RespondTooManyRequests(context, retryAfter);
            }
        }

        // Don't advertise server software and versions.
        void Application_PreSendRequestHeaders(object sender, EventArgs e)
        {
            var headers = HttpContext.Current?.Response?.Headers;
            if (headers == null) return;
            headers.Remove("Server");
            headers.Remove("X-AspNet-Version");
            headers.Remove("X-AspNetMvc-Version");
        }

        void Session_Start(object sender, EventArgs e)
        {
            Session.Timeout = Math.Max(1, AppSettings.SessionLifetimeSeconds / 60);
            // Storing a value also keeps the session ID stable, which ViewStateUserKey relies on.
            CsrfCookie.EnsureToken(Context);
        }

        // Double-submit CSRF check for every state-changing request: the AntiCsrfToken cookie (or X-CSRF-Token header)
        // must match the session's copy. The login page checks its own form token instead.
        protected void Application_AcquireRequestState(object sender, EventArgs e)
        {
            var context = HttpContext.Current;
            if (context?.Session == null) return;
            string method = context.Request.HttpMethod;
            if (method != "POST" && method != "PUT" && method != "DELETE") return;
            if (context.Request.Path.EndsWith("/Pages/Login/LoginPage.aspx", StringComparison.OrdinalIgnoreCase)) return;

            string sessionToken = context.Session[CsrfCookie.Name] as string;
            string requestToken = context.Request.Headers["X-CSRF-Token"] ?? context.Request.Cookies[CsrfCookie.Name]?.Value;

            if (string.IsNullOrEmpty(sessionToken))
            {
                // Session expired between loading the page and submitting it.
                context.Response.Redirect("~/Pages/Login/LoginPage.aspx?reason=expired", false);
                context.ApplicationInstance.CompleteRequest();
                return;
            }
            if (!SecurityHelpers.FixedTimeEquals(sessionToken, requestToken))
            {
                SecureLogger.Security("csrf.blocked", "POST rejected: CSRF token missing or wrong");
                context.Response.StatusCode = 403;
                context.Response.TrySkipIisCustomErrors = true;
                context.ApplicationInstance.CompleteRequest();
            }
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            var context = HttpContext.Current;
            string requestId = RequestInfo.RequestId(context);
            int status = (ex as HttpException)?.GetHttpCode() ?? 500;

            if (ex is HttpRequestValidationException)
                SecureLogger.Security("request.validation_failed", "Request blocked by ASP.NET request validation (possible script injection)");
            else if (status == 404)
                SecureLogger.LogWarning("http.not_found", "Page not found");
            else
                SecureLogger.LogError("app.unhandled_error", "Unhandled application error", ex?.GetBaseException(), new { status });

            Server.ClearError();
            string path = context?.Request?.Path ?? "";
            if (path.IndexOf("/Pages/Error/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // The error page itself failed: show plain text instead of redirecting in a loop.
                Response.Clear();
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                Response.Write("An unexpected error occurred. Reference: " + HttpUtility.HtmlEncode(requestId));
                CompleteRequest();
                return;
            }

            // Only a generic message, the error type and the request reference go in the URL; details stay in the log.
            string code = status == 404 ? "NotFound" : ex?.GetBaseException().GetType().Name ?? "Unknown";
            Response.Redirect($"~/Pages/Error/ErrorPage.aspx?code={HttpUtility.UrlEncode(code)}&ref={HttpUtility.UrlEncode(requestId)}", false);
            CompleteRequest();
        }
    }
}

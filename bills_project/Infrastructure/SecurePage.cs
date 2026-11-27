using System;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using bills_project.Services;

namespace bills_project.Infrastructure
{
    // Base class for every page that needs a signed-in user. It replaces the session, CSRF and
    // admin checks that used to be copy-pasted into each page.
    //
    // CSRF protection has two layers:
    //  1. ViewStateUserKey ties each page's view state to the session, so a postback forged from
    //     another site fails ASP.NET's view state validation.
    //  2. Global.asax compares the AntiCsrfToken cookie with the session copy on every POST.
    public abstract class SecurePage : Page
    {
        public const string SessionUserKey = "AuthUser";

        private IDatabaseManager _db;

        // Pages that only admins may open override this.
        protected virtual bool RequiresAdmin => false;

        // Opened on first use and closed when the page unloads.
        protected IDatabaseManager Db => _db ?? (_db = new DatabaseManager());

        protected string CurrentUserId => User.Identity.Name;
        protected bool IsAdmin => SecurityHelpers.IsAdmin(Session["IsAdmin"]);
        protected string ClientIp => RequestInfo.ClientIp(Context);

        protected override void OnInit(EventArgs e)
        {
            if (Session != null)
                ViewStateUserKey = Session.SessionID;
            base.OnInit(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            if (!User.Identity.IsAuthenticated)
            {
                Response.Redirect(FormsAuthentication.LoginUrl, true);
                return;
            }

            // The session must belong to the user in the auth cookie. A missing value means the session
            // expired (or the app restarted); a different value means the cookies don't belong together.
            string sessionUser = Session[SessionUserKey] as string;
            if (!string.Equals(sessionUser, User.Identity.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (sessionUser != null)
                    SecureLogger.Security("session.user_mismatch", "Session belongs to a different user than the auth cookie", new { sessionUser });
                FormsAuthentication.SignOut();
                Session.Clear();
                Response.Redirect("~/Pages/Login/LoginPage.aspx?reason=expired", true);
                return;
            }

            if (RequiresAdmin && !IsAdmin)
            {
                SecureLogger.Security("access.denied", "Non-admin user tried to open an admin page");
                Response.Redirect("~/Pages/Error/ErrorPage.aspx?code=AccessDenied", true);
                return;
            }

            base.OnLoad(e);
        }

        protected override void OnPreRender(EventArgs e)
        {
            CsrfCookie.Issue(Context);
            base.OnPreRender(e);
        }

        protected override void OnUnload(EventArgs e)
        {
            _db?.Dispose();
            _db = null;
            base.OnUnload(e);
        }

        // Redirect without throwing ThreadAbortException; use as the last statement of a handler.
        protected void RedirectTo(string url)
        {
            Response.Redirect(url, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        // The error page shows a fixed message for each code (see ErrorPage.aspx.cs) plus this request's reference.
        protected void RedirectToError(string code)
        {
            RedirectTo($"~/Pages/Error/ErrorPage.aspx?code={HttpUtility.UrlEncode(code)}&ref={RequestInfo.RequestId(Context)}");
        }

        // Applies a per-user rate limit; returns a user-facing message when blocked, otherwise null.
        protected string CheckRateLimit(string bucket, RateLimitPolicy policy)
        {
            return RateLimits.Allow(bucket, CurrentUserId, policy, out var retryAfter)
                ? null
                : $"You're doing that too often. Please try again in {RateLimits.Describe(retryAfter)}.";
        }
    }

    // The anti-CSRF token: one random value per session, kept in the session and in an HttpOnly cookie.
    public static class CsrfCookie
    {
        public const string Name = "AntiCsrfToken";

        public static string EnsureToken(HttpContext context)
        {
            var session = context.Session;
            if (session == null) return null;
            if (!(session[Name] is string token))
            {
                token = Guid.NewGuid().ToString("N");
                session[Name] = token;
            }
            return token;
        }

        // Replaces the token, e.g. after login, so a token seen before authentication can't be reused.
        public static void Rotate(HttpContext context)
        {
            if (context.Session == null) return;
            context.Session[Name] = Guid.NewGuid().ToString("N");
            Issue(context);
        }

        public static void Issue(HttpContext context)
        {
            string token = EnsureToken(context);
            if (token == null) return;
            context.Response.Cookies.Set(new HttpCookie(Name, token)
            {
                HttpOnly = true,
                Secure = context.Request.IsSecureConnection,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
        }
    }
}

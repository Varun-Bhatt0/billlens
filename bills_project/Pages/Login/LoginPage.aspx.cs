using System;
using System.Web.Security;
using System.Web.UI;
using bills_project.App_Config;
using bills_project.Infrastructure;
using bills_project.Services;

namespace bills_project.Pages.Login
{
    // Login page. Anonymous, so it doesn't derive from SecurePage; it checks its own form token
    // (Global.asax skips its CSRF cookie check for this page).
    public partial class LoginPage : Page
    {
        protected override void OnInit(EventArgs e)
        {
            if (Session != null)
                ViewStateUserKey = Session.SessionID;
            base.OnInit(e);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            string token = CsrfCookie.EnsureToken(Context);
            phDemo.Visible = AppSettings.SeedDemoUsers;
            if (!IsPostBack)
            {
                CsrfToken.Value = token;
                if (Request.QueryString["reason"] == "expired" || Request.QueryString["session"] == "expired")
                    ShowMessage("Your session has expired. Please log in again.");
            }
        }

        private bool IsValidFormToken()
        {
            var sessionToken = Session[CsrfCookie.Name] as string;
            return !string.IsNullOrEmpty(sessionToken) && SecurityHelpers.FixedTimeEquals(sessionToken, CsrfToken.Value);
        }

        private void ShowMessage(string text)
        {
            MessagePanel.Visible = true;
            MessageLiteral.Text = Server.HtmlEncode(text);
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            if (!IsValidFormToken())
            {
                SecureLogger.Security("csrf.blocked", "Login form token missing or wrong");
                ShowMessage(AppSettings.Message("SESSION_EXPIRED", "Session expired. Please login again"));
                CsrfToken.Value = CsrfCookie.EnsureToken(Context);
                return;
            }

            string employeeId = txtEmployeeId.Text.Trim();
            string ip = RequestInfo.ClientIp(Context);

            // Per-IP limit on login attempts (separate from the per-account lockout in AuthService).
            if (!RateLimits.Allow("login", ip, RateLimits.Login, out var retryAfter))
            {
                ShowMessage($"Too many login attempts. Please try again in {RateLimits.Describe(retryAfter)}.");
                return;
            }

            AuthResult result;
            using (var db = new DatabaseManager())
            {
                var auth = new AuthService(db, RateLimits.LoginFailures, AppSettings.LoginLockout);
                result = auth.Authenticate(employeeId, txtPassword.Text);

                if (result.Succeeded)
                {
                    db.UpdateLastLogin(employeeId);
                    db.LogAudit(employeeId, "Login", "Signed in");
                }
                else if (result.Outcome == AuthOutcome.LockedOut)
                {
                    db.LogAudit(employeeId, "LoginLocked", "Login refused: account temporarily locked");
                }
            }

            switch (result.Outcome)
            {
                case AuthOutcome.Success:
                    SecureLogger.Security("auth.login_succeeded", "User signed in", new { employeeId }, SecureLogger.Level.Info);
                    Session[SecurePage.SessionUserKey] = employeeId;
                    Session["UserName"] = result.User["emp_name"];
                    Session["IsAdmin"] = result.User["is_admin"];
                    CsrfCookie.Rotate(Context);
                    FormsAuthentication.SetAuthCookie(employeeId, false);
                    Response.Redirect("~/Pages/Home/HomeOptions.aspx", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;

                case AuthOutcome.LockedOut:
                    SecureLogger.Security("auth.locked_out", "Login refused: account locked", new { employeeId, retryAfterSeconds = (int)result.RetryAfter.TotalSeconds });
                    ShowMessage($"Too many failed attempts for this account. Please try again in {RateLimits.Describe(result.RetryAfter)}.");
                    return;

                default:
                    SecureLogger.Security("auth.login_failed", "Invalid employee ID or password", new { employeeId });
                    ShowMessage(AppSettings.Message("LOGIN_FAILED", "Invalid employee ID or password"));
                    return;
            }
        }
    }
}

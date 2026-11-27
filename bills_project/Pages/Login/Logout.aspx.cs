using System;
using System.Web;
using System.Web.Security;
using bills_project.Infrastructure;
using bills_project.Services;

namespace bills_project.Pages.Login
{
    // Clears the session and auth cookie, then returns to the login page.
    public partial class Logout : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (User.Identity.IsAuthenticated)
            {
                string employeeId = User.Identity.Name;
                SecureLogger.Security("auth.logout", "User signed out", new { employeeId }, SecureLogger.Level.Info);
                try
                {
                    using (var db = new DatabaseManager())
                        db.LogAudit(employeeId, "Logout", "Signed out");
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("audit.logout_failed", "Could not write logout audit entry", ex);
                }
            }

            Session.Clear();
            Session.Abandon();
            FormsAuthentication.SignOut();
            Expire("ASP.NET_SessionId");
            Expire(CsrfCookie.Name);

            Response.Redirect(FormsAuthentication.LoginUrl, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void Expire(string name)
        {
            Response.Cookies.Add(new HttpCookie(name, "") { Expires = DateTime.UtcNow.AddDays(-1), Path = "/" });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace bills_project.Pages.Error
{
    // Generic error page. The message comes from a fixed list keyed by the error code, so a crafted link
    // can't make the site display arbitrary text. Details are only in the server log, under the reference ID.
    public partial class ErrorPage : System.Web.UI.Page
    {
        private static readonly Dictionary<string, string> Messages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AccessDenied", "You do not have permission to view this page." },
            { "NotFound", "The page you were looking for doesn't exist." },
            { "SaveError", "The bill could not be saved. Please try again later." },
        };

        private static readonly Regex SafeToken = new Regex("^[A-Za-z0-9_.-]{1,64}$");

        protected void Page_Load(object sender, EventArgs e)
        {
            string code = Request.QueryString["code"];
            if (string.IsNullOrWhiteSpace(code) || !SafeToken.IsMatch(code))
                code = "Unknown";
            string reference = Request.QueryString["ref"];

            lblErrorMessage.Text = Server.HtmlEncode(Messages.TryGetValue(code, out var msg)
                ? msg
                : "An unexpected error occurred. Please try again or contact support.");
            lblErrorCode.Text = "Error code: " + Server.HtmlEncode(code)
                + (!string.IsNullOrEmpty(reference) && SafeToken.IsMatch(reference) ? " · Reference: " + Server.HtmlEncode(reference) : "");
        }
    }
}

using System;
using System.Web.UI.WebControls;
using bills_project.Infrastructure;
using bills_project.Services;

namespace bills_project
{
    // Shared layout: navigation bar for signed-in users, with the current page highlighted.
    public partial class SiteMaster : System.Web.UI.MasterPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            bool signedIn = Page.User.Identity.IsAuthenticated && Session?[SecurePage.SessionUserKey] != null;
            phNav.Visible = signedIn;
            if (!signedIn) return;

            bool isAdmin = SecurityHelpers.IsAdmin(Session["IsAdmin"]);
            litUserName.Text = Server.HtmlEncode(Session["UserName"] as string ?? Page.User.Identity.Name);
            lblAdminBadge.Visible = isAdmin;
            lnkAudit.Visible = isAdmin;

            foreach (var link in new[] { lnkHome, lnkUpload, lnkManual, lnkBills, lnkAudit })
                MarkActive(link);
        }

        private void MarkActive(HyperLink link)
        {
            string target = ResolveUrl(link.NavigateUrl);
            if (!string.Equals(Request.Path, target, StringComparison.OrdinalIgnoreCase)) return;
            link.CssClass += " active";
            link.Attributes["aria-current"] = "page";
        }
    }
}

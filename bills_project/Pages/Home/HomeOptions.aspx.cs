using System;
using bills_project.Infrastructure;
using bills_project.Services;

namespace bills_project.Pages.Home
{
    // Home page: a short summary of the user's bills and links to the three main actions.
    public partial class HomeOptions : SecurePage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            lblUserName.Text = Server.HtmlEncode(Session["UserName"] as string ?? CurrentUserId);
            if (IsPostBack) return;

            var summary = BillSummary.From(Db.GetBillsForUser(CurrentUserId));
            litBillCount.Text = summary.Count.ToString();
            litTotalAmount.Text = summary.TotalText;
            litLastSubmitted.Text = summary.Count > 0 ? Server.HtmlEncode(summary.LastSubmittedText) : "&mdash;";
        }
    }
}

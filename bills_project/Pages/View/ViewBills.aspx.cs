using System;
using System.Data;
using System.Web.UI.WebControls;
using bills_project.Infrastructure;
using bills_project.Services;

namespace bills_project.Pages.ViewBills
{
    // The signed-in user's bill history with summary cards.
    public partial class ViewBills : SecurePage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;
            pnlSaved.Visible = Request.QueryString["saved"] == "1";
            Db.LogAudit(CurrentUserId, "ViewBills", "Viewed bill history");
            BindBills();
        }

        private void BindBills()
        {
            DataTable dt = Db.GetBillsForUser(CurrentUserId);
            gvBills.DataSource = dt;
            gvBills.DataBind();
            if (gvBills.HeaderRow != null) gvBills.HeaderRow.TableSection = TableRowSection.TableHeader;

            var summary = BillSummary.From(dt);
            pnlSummary.Visible = summary.Count > 0;
            litTotalBills.Text = summary.Count.ToString();
            litTotalAmount.Text = summary.TotalText;
            litLatestSubmission.Text = Server.HtmlEncode(summary.LastSubmittedText);
        }

        protected void gvBills_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvBills.PageIndex = e.NewPageIndex;
            BindBills();
        }
    }
}

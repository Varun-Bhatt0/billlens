using System;
using System.Web.UI.WebControls;
using bills_project.App_Config;
using bills_project.Infrastructure;
using bills_project.Services;

namespace bills_project.Pages.ManualEntry
{
    // Enter a bill's details by hand, without uploading a file.
    public partial class ManualEntry : SecurePage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ddlBillType.Items.Clear();
                ddlBillType.Items.Add(new ListItem("Choose a bill type", ""));
                foreach (var bt in Db.GetBillTypes())
                    ddlBillType.Items.Add(new ListItem(bt.Value, bt.Key.ToString()));
                if (ddlBillType.Items.Count == 1)
                    ShowError("Could not load bill types. Please try again later.");
            }
        }

        private void ShowError(string text)
        {
            pnlError.Visible = true;
            litError.Text = Server.HtmlEncode(text);
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            string limited = CheckRateLimit("save", RateLimits.Save);
            if (limited != null)
            {
                ShowError(limited);
                return;
            }

            if (!int.TryParse(ddlBillType.SelectedValue, out int billTypeId))
            {
                ShowError("Please select a bill type.");
                return;
            }

            bool saved;
            try
            {
                // Stored as typed; output is HTML-encoded where it is displayed.
                saved = new BillService(Db).SaveBill(CurrentUserId, billTypeId, txtBillNumber.Text, txtBillDate.Text, txtBillAmt.Text, null);
            }
            catch (ArgumentException ex)
            {
                ShowError(ex.Message);
                return;
            }

            if (saved)
                RedirectTo("~/Pages/View/ViewBills.aspx");
            else
                ShowError(AppSettings.Message("DATABASE_ERROR", "Could not save the bill. Please try again later."));
        }
    }
}

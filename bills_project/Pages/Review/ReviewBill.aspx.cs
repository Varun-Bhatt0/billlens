using System;
using System.Web.UI.WebControls;
using bills_project.Infrastructure;
using bills_project.Services;

namespace bills_project.Pages
{
    // Shows the OCR results handed over by UploadBill (via the session), flags fields that were hard to read,
    // lets the user correct them, and saves the bill.
    public partial class ReviewBill : SecurePage
    {
        // On the benchmark, fields at or above this confidence were correct about 96% of the time.
        private const double ConfidentThreshold = 0.6;

        private static readonly string[] ReviewKeys =
            { "BillType", "BillTypeText", "BillNo", "BillDate", "BillAmt", "FilePath", "OCR", "OcrError", "ConfNo", "ConfDate", "ConfAmt", "CrossChecked" };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            string billType = Session["ReviewBill_BillType"] as string;
            if (string.IsNullOrEmpty(billType))
            {
                // No upload in progress
                RedirectTo("~/Pages/Upload/UploadBill.aspx");
                return;
            }

            lblBillType.Text = Server.HtmlEncode(Session["ReviewBill_BillTypeText"] as string ?? billType);
            txtBillNo.Text = Session["ReviewBill_BillNo"] as string ?? "";
            txtBillDate.Text = Session["ReviewBill_BillDate"] as string ?? "";
            txtAmount.Text = Session["ReviewBill_BillAmt"] as string ?? "";

            bool crossChecked = Session["ReviewBill_CrossChecked"] as bool? ?? false;
            MarkField(txtBillNo, litNoHint, Conf("ConfNo"), null);
            MarkField(txtBillDate, litDateHint, Conf("ConfDate"), null);
            MarkField(txtAmount, litAmtHint, Conf("ConfAmt"), crossChecked ? "matches sub-total + tax" : null);

            ShowSummary(Session["ReviewBill_OCR"] as string ?? "low", Session["ReviewBill_OcrError"] as string ?? "");
        }

        private double Conf(string key) => Session["ReviewBill_" + key] as double? ?? 0;

        // Empty or low-confidence fields get a warning border and a "check" badge; confident ones a quiet "read" badge.
        private static void MarkField(TextBox box, Literal hint, double confidence, string verifiedNote)
        {
            if (string.IsNullOrEmpty(box.Text))
            {
                box.CssClass += " field-check";
                hint.Text = "<span class=\"badge text-bg-warning\">not found</span>";
            }
            else if (confidence < ConfidentThreshold)
            {
                box.CssClass += " field-check";
                hint.Text = "<span class=\"badge text-bg-warning\">check</span>";
            }
            else
            {
                hint.Text = verifiedNote != null
                    ? $"<span class=\"badge text-bg-success\"><i class=\"fas fa-check me-1\"></i>{verifiedNote}</span>"
                    : "<span class=\"badge text-bg-light border\">read from file</span>";
            }
        }

        private void ShowSummary(string confidence, string ocrError)
        {
            switch (confidence)
            {
                case "high":
                    SetAlert("success", "All fields were read from the bill. Have a quick look, then save.");
                    break;
                case "medium":
                    SetAlert("warning", "Some fields were hard to read. Please check the highlighted ones.");
                    break;
                default:
                    SetAlert("danger", "Most fields could not be read. Please enter the bill details.");
                    break;
            }
            if (!string.IsNullOrEmpty(ocrError))
                litAlert.Text += " <span class=\"small\">(" + Server.HtmlEncode(ocrError) + ")</span>";
        }

        private void SetAlert(string kind, string text)
        {
            pnlAlert.Visible = true;
            pnlAlert.CssClass = $"alert alert-{kind}";
            litAlert.Text = Server.HtmlEncode(text);
        }

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;

            string limited = CheckRateLimit("save", RateLimits.Save);
            if (limited != null)
            {
                SetAlert("danger", limited);
                return;
            }

            // The bill type and file come from this session's upload, never from values posted back by the browser.
            string billType = Session["ReviewBill_BillType"] as string;
            string filePath = Session["ReviewBill_FilePath"] as string;
            if (!int.TryParse(billType, out int billTypeId))
            {
                RedirectTo("~/Pages/Upload/UploadBill.aspx");
                return;
            }

            bool saved;
            try
            {
                saved = new BillService(Db).SaveBill(CurrentUserId, billTypeId, txtBillNo.Text, txtBillDate.Text, txtAmount.Text, filePath);
            }
            catch (ArgumentException ex)
            {
                SetAlert("danger", ex.Message);
                return;
            }

            if (saved)
            {
                foreach (var key in ReviewKeys) Session.Remove("ReviewBill_" + key);
                RedirectTo("~/Pages/View/ViewBills.aspx?saved=1");
            }
            else
            {
                RedirectToError("SaveError");
            }
        }
    }
}

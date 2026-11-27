using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI;
using System.Web.UI.WebControls;
using bills_project.App_Config;
using bills_project.Infrastructure;
using bills_project.Services;

namespace bills_project.Pages.Upload
{
    // Upload a bill file, read it with OCR, and hand the results to the Review page.
    // OCR runs as an async page task (Async="true"), so no request thread is blocked while Tesseract works.
    public partial class UploadBill : SecurePage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            litMaxSize.Text = AppSettings.MaxContentLengthMB.ToString();
            if (IsPostBack) return;

            ddlBillType.Items.Clear();
            ddlBillType.Items.Add(new ListItem("Choose a bill type", ""));
            foreach (var bt in Db.GetBillTypes())
                ddlBillType.Items.Add(new ListItem(bt.Value, bt.Key.ToString()));
            if (ddlBillType.Items.Count == 1)
                ShowError("Could not load bill types. Please try again later.");
        }

        private void ShowError(string text)
        {
            pnlError.Visible = true;
            litError.Text = Server.HtmlEncode(text);
        }

        protected void btnProcessOCR_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid || !fuBill.HasFile)
            {
                ShowError("Please choose a bill type and a file.");
                return;
            }
            if (!int.TryParse(ddlBillType.SelectedValue, out _))
            {
                ShowError("Please choose a bill type.");
                return;
            }

            string limited = CheckRateLimit("upload", RateLimits.Upload);
            if (limited != null)
            {
                ShowError(limited);
                return;
            }

            // Validate name, size and content before anything touches the disk.
            string fileExt = Path.GetExtension(Path.GetFileName(fuBill.FileName)).ToLowerInvariant();
            var allowed = AppSettings.AllowedExtensions;
            int maxMb = AppSettings.MaxContentLengthMB;
            if (!allowed.Contains(fileExt))
            {
                SecureLogger.LogWarning("upload.rejected", "File type not allowed", new { extension = fileExt });
                ShowError(AppSettings.Message("INVALID_FILE_TYPE", "Invalid file type. Allowed types: {}").Replace("{}", string.Join(" ", allowed)));
                return;
            }
            if (fuBill.PostedFile.ContentLength > maxMb * 1024 * 1024)
            {
                SecureLogger.LogWarning("upload.rejected", "File too large", new { bytes = fuBill.PostedFile.ContentLength });
                ShowError(AppSettings.Message("FILE_TOO_LARGE", "File too large. Maximum size allowed: {}MB").Replace("{}", maxMb.ToString()));
                return;
            }
            if (!FileSignature.Matches(fileExt, fuBill.PostedFile.InputStream))
            {
                SecureLogger.Security("upload.signature_mismatch", "File content doesn't match its extension", new { extension = fileExt });
                ShowError("That file doesn't look like a real " + fileExt.TrimStart('.').ToUpperInvariant() + " file.");
                return;
            }

            string savePath;
            try
            {
                // Stored under a random name in App_Data (never served by IIS); the original name isn't used on disk.
                string billsDir = AppSettings.MapPath(AppSettings.UploadFolder);
                Directory.CreateDirectory(billsDir);
                savePath = Path.Combine(billsDir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{fileExt}");
                fuBill.SaveAs(savePath);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("upload.save_failed", "Could not store uploaded file", ex);
                ShowError(AppSettings.Message("GENERAL_ERROR", "An error occurred. Please try again"));
                return;
            }

            string billType = ddlBillType.SelectedValue;
            string billTypeText = ddlBillType.SelectedItem?.Text ?? "";
            RegisterAsyncTask(new PageAsyncTask(ct => ReadBillAsync(savePath, billType, billTypeText, ct)));
        }

        private async Task ReadBillAsync(string savePath, string billType, string billTypeText, CancellationToken ct)
        {
            var ocr = await new OcrProcessor().ProcessAsync(savePath, ct);
            SecureLogger.LogInfo("upload.processed", "Bill uploaded and read", new { file = Path.GetFileName(savePath), confidence = ocr.Confidence });

            Session["ReviewBill_BillType"] = billType;
            Session["ReviewBill_BillTypeText"] = billTypeText;
            Session["ReviewBill_BillNo"] = ocr.BillNo;
            Session["ReviewBill_BillDate"] = BillFormat.NormalizeDate(ocr.BillDate);
            Session["ReviewBill_BillAmt"] = BillFormat.NormalizeAmount(ocr.BillAmount);
            Session["ReviewBill_FilePath"] = savePath;
            Session["ReviewBill_OCR"] = ocr.Confidence;
            Session["ReviewBill_OcrError"] = ocr.Error;
            Session["ReviewBill_ConfNo"] = ocr.BillNoConfidence;
            Session["ReviewBill_ConfDate"] = ocr.DateConfidence;
            Session["ReviewBill_ConfAmt"] = ocr.AmountConfidence;
            Session["ReviewBill_CrossChecked"] = ocr.AmountCrossChecked;

            RedirectTo("~/Pages/Review/ReviewBill.aspx");
        }
    }
}

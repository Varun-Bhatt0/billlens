using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using bills_project.Services;
using bills_project.Services.Ocr;

namespace OcrBenchmark
{
    // The original OCR approach, kept only to measure the improvement against:
    // no preprocessing, Tesseract's default page segmentation (psm 3), plain text output,
    // and the first match of three regular expressions.
    public static class LegacyBaseline
    {
        private static readonly string[] InvoicePatterns =
        {
            @"Invoice\s*(?:No\.?|Number)?\s*[:#]?\s*([\w\-\/]+)",
            @"Bill\s*(?:No\.?|Number)?\s*[:#]?\s*([\w\-\/]+)",
            @"Receipt\s*(?:No\.?|Number)?\s*[:#]?\s*([\w\-\/]+)"
        };
        private const string DatePattern = @"(?:Invoice\s*Date|Bill\s*Date|Date)\s*[:#]?\s*(\d{1,2}[\/\.-]\d{1,2}[\/\.-]\d{2,4})";
        private const string AmountPattern = @"(?:Bill\s*Amount|Total\s*Amount|Amount)\s*[:#]?\s*(?:Rs\.?|₹|INR)?\s*(\d+(?:,\d{3})*(?:\.\d{1,2})?)";

        public static async Task<ParsedBill> RunAsync(string imagePath, OcrEngineOptions options, CancellationToken ct)
        {
            string text = await ProcessRunner.RunAsync(options.TesseractPath, $"\"{imagePath}\" stdout -l {options.Language}", options.TimeoutSeconds, ct);
            var bill = new ParsedBill();
            foreach (var p in InvoicePatterns)
            {
                var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
                if (m.Success) { bill.BillNo = m.Groups[1].Value.Trim(); break; }
            }
            var d = Regex.Match(text, DatePattern, RegexOptions.IgnoreCase);
            if (d.Success) bill.BillDate = BillFormat.NormalizeDate(d.Groups[1].Value.Trim());
            var a = Regex.Match(text, AmountPattern, RegexOptions.IgnoreCase);
            if (a.Success) bill.BillAmount = BillFormat.NormalizeAmount(a.Groups[1].Value.Replace(",", ""));
            return bill;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using bills_project.App_Config;
using bills_project.Services.Ocr;

namespace bills_project.Services
{
    public class OcrResult
    {
        public string BillNo { get; set; } = "";
        public string BillDate { get; set; } = "";      // yyyy-MM-dd
        public string BillAmount { get; set; } = "";
        public double BillNoConfidence { get; set; }
        public double DateConfidence { get; set; }
        public double AmountConfidence { get; set; }
        public bool AmountCrossChecked { get; set; }
        public string Confidence { get; set; } = "low";  // high / medium / low
        public string Error { get; set; } = "";
    }

    // Web-app entry point to the OCR pipeline (Services/Ocr): decodes the upload (System.Drawing for images,
    // Poppler for PDFs), runs the pipeline asynchronously and logs what happened.
    public class OcrProcessor
    {
        private readonly OcrEngineOptions _engine = new OcrEngineOptions
        {
            TesseractPath = AppSettings.TesseractPath,
            PopplerPath = AppSettings.PopplerPath,
            TimeoutSeconds = AppSettings.OcrTimeoutSeconds
        };

        public async Task<OcrResult> ProcessAsync(string filePath, CancellationToken ct = default)
        {
            var result = new OcrResult();
            string workDir = Path.Combine(Path.GetTempPath(), "bills_ocr_" + Guid.NewGuid().ToString("N"));
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return Fail(result, "File not found.");
                if (string.IsNullOrEmpty(_engine.TesseractPath))
                    return Fail(result, "Tesseract is not installed or TESSERACT_PATH is wrong.");

                Directory.CreateDirectory(workDir);
                var runner = new TesseractRunner(_engine);
                var pages = new List<GrayImage>();
                if (Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var pagePath in await runner.RenderPdfAsync(filePath, workDir, ct).ConfigureAwait(false))
                        pages.Add(GrayImage.ReadPgm(pagePath));
                    if (pages.Count == 0) return Fail(result, "Could not read the PDF (is Poppler installed?).");
                }
                else
                {
                    pages.Add(LoadImage(filePath));
                }

                var outcome = await new OcrPipeline(runner).RunAsync(pages, workDir, ct).ConfigureAwait(false);
                var bill = outcome.Bill;
                result.BillNo = bill.BillNo;
                result.BillDate = bill.BillDate;
                result.BillAmount = bill.BillAmount;
                result.BillNoConfidence = bill.BillNoConfidence;
                result.DateConfidence = bill.DateConfidence;
                result.AmountConfidence = bill.AmountConfidence;
                result.AmountCrossChecked = bill.AmountCrossChecked;
                result.Confidence = bill.Confidence;
                if (bill.FieldsFound == 0) result.Error = "No bill details could be read from the file.";

                SecureLogger.LogInfo("ocr.completed", "OCR finished", new
                {
                    pages = pages.Count,
                    fields = bill.FieldsFound,
                    confidence = bill.Confidence,
                    crossChecked = bill.AmountCrossChecked,
                    psm = outcome.PageSegMode,
                    passes = outcome.PassesRun,
                    skew = outcome.SkewDegrees,
                    scale = Math.Round(outcome.ScaleFactor, 2),
                    wordConfidence = Math.Round(outcome.MeanWordConfidence, 1),
                    ms = outcome.ElapsedMs
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException ex)
            {
                SecureLogger.LogError("ocr.timeout", "OCR timed out", ex);
                Fail(result, "Reading the file took too long.");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                SecureLogger.LogError("ocr.engine_missing", "OCR engine could not be started", ex);
                Fail(result, "The OCR engine could not be started.");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ocr.failed", "Unexpected OCR error", ex);
                Fail(result, "The file could not be read.");
            }
            finally
            {
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); }
                catch (Exception ex) { SecureLogger.LogWarning("ocr.cleanup_failed", "Could not delete OCR temp folder", new { error = ex.Message }); }
            }
            return result;
        }

        private static OcrResult Fail(OcrResult result, string error)
        {
            result.Error = error;
            result.Confidence = "low";
            return result;
        }

        // Decodes a PNG/JPEG into grayscale. Reads through a memory copy so the upload is never left locked.
        private static GrayImage LoadImage(string path)
        {
            using (var ms = new MemoryStream(File.ReadAllBytes(path)))
            using (var source = Image.FromStream(ms))
            using (var bmp = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White); // transparent PNGs become white paper, not black
                    g.DrawImage(source, 0, 0, source.Width, source.Height);
                }
                var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
                var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    var row = new byte[data.Stride];
                    var gray = new GrayImage(bmp.Width, bmp.Height);
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, data.Stride);
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            int b = row[x * 3], gch = row[x * 3 + 1], r = row[x * 3 + 2];
                            gray[x, y] = (byte)((r * 299 + gch * 587 + b * 114) / 1000); // ITU-R BT.601 luma
                        }
                    }
                    return gray;
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
        }
    }
}

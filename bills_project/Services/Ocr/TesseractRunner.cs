using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace bills_project.Services.Ocr
{
    public sealed class OcrEngineOptions
    {
        public string TesseractPath { get; set; } = "tesseract";
        public string PopplerPath { get; set; }           // folder containing pdftoppm; null = on PATH
        public string Language { get; set; } = "eng";
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxPdfPages { get; set; } = 3;
    }

    // Runs external tools (Tesseract, pdftoppm) asynchronously with a timeout and cancellation.
    public sealed class TesseractRunner
    {
        private readonly OcrEngineOptions _options;

        public TesseractRunner(OcrEngineOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        // Page segmentation modes tried by the pipeline:
        //   6 = a single uniform block of text (best for most receipts and simple bills)
        //   4 = a single column of text of variable sizes
        //  11 = sparse text, find as much as possible in no particular order (forms, scattered fields)
        public Task<string> RecognizeTsvAsync(string imagePath, int pageSegMode, CancellationToken ct)
        {
            // --oem 1: LSTM engine only. preserve_interword_spaces keeps column gaps from merging words.
            string args = $"\"{imagePath}\" stdout --oem 1 --psm {pageSegMode} -l {_options.Language} " +
                          "-c preserve_interword_spaces=1 tsv";
            return RunAsync(_options.TesseractPath, args, ct);
        }

        // Renders the first pages of a PDF to grayscale PGM files at 300 DPI; returns their paths in page order.
        public async Task<string[]> RenderPdfAsync(string pdfPath, string outputDir, CancellationToken ct)
        {
            string exe = string.IsNullOrEmpty(_options.PopplerPath) ? "pdftoppm" : Path.Combine(_options.PopplerPath, "pdftoppm");
            string prefix = Path.Combine(outputDir, "page");
            await RunAsync(exe, $"-f 1 -l {_options.MaxPdfPages} -r 300 -gray \"{pdfPath}\" \"{prefix}\"", ct).ConfigureAwait(false);
            var pages = Directory.GetFiles(outputDir, "page-*.pgm");
            Array.Sort(pages, StringComparer.Ordinal);
            return pages;
        }

        private async Task<string> RunAsync(string fileName, string arguments, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (var process = new Process { StartInfo = psi, EnableRaisingEvents = true })
            {
                var exited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                process.Exited += (s, e) => exited.TrySetResult(true);
                process.Start();
                // Read both streams concurrently so a full pipe buffer can't stall the tool.
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();

                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
                    using (timeout.Token.Register(() => exited.TrySetCanceled()))
                    {
                        try
                        {
                            await exited.Task.ConfigureAwait(false);
                        }
                        catch (TaskCanceledException)
                        {
                            try { process.Kill(); } catch (InvalidOperationException) { /* already exited */ }
                            if (ct.IsCancellationRequested) throw new OperationCanceledException(ct);
                            throw new TimeoutException($"{Path.GetFileName(fileName)} did not finish within {_options.TimeoutSeconds}s.");
                        }
                    }
                }

                string output = await stdout.ConfigureAwait(false);
                string errors = await stderr.ConfigureAwait(false);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"{Path.GetFileName(fileName)} exited with code {process.ExitCode}: {Truncate(errors, 300)}");
                return output;
            }
        }

        private static string Truncate(string s, int max) => s == null || s.Length <= max ? s : s.Substring(0, max);
    }
}

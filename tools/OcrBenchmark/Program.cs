using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using bills_project.Services;
using bills_project.Services.Ocr;

namespace OcrBenchmark
{
    // Usage:
    //   dotnet run -c Release -- --data ../ocr-benchmark/data [--mode both|baseline|improved] [--limit N]
    //                            [--parallel 2] [--report results.md] [--min-accuracy 0.9] [--tesseract path]
    // Exit code 2 if field accuracy of the improved pipeline is below --min-accuracy (used as a CI gate).
    public static class Program
    {
        private sealed class Truth
        {
            public string id { get; set; }
            public string template { get; set; }
            public int severity { get; set; }
            public string bill_no { get; set; }
            public string date { get; set; }
            public string amount { get; set; }
        }

        private sealed class SampleResult
        {
            public Truth Truth;
            public ParsedBill Bill;
            public long Ms;
            public int Passes;
            public string Error;
            public bool NoOk => Eq(Truth.bill_no, Bill?.BillNo);
            public bool DateOk => Truth.date == Bill?.BillDate;
            public bool AmountOk => AmountEq(Truth.amount, Bill?.BillAmount);
            public bool AllOk => NoOk && DateOk && AmountOk;
        }

        public static async Task<int> Main(string[] args)
        {
            var opt = ParseArgs(args);
            string data = opt.GetValueOrDefault("data", "data");
            string mode = opt.GetValueOrDefault("mode", "both");
            int limit = int.Parse(opt.GetValueOrDefault("limit", "100000"));
            int parallel = int.Parse(opt.GetValueOrDefault("parallel", Environment.ProcessorCount.ToString()));
            double minAccuracy = double.Parse(opt.GetValueOrDefault("min-accuracy", "0"), CultureInfo.InvariantCulture);
            var engine = new OcrEngineOptions { TesseractPath = opt.GetValueOrDefault("tesseract", "tesseract"), TimeoutSeconds = 120 };

            var truths = JsonSerializer.Deserialize<List<Truth>>(File.ReadAllText(Path.Combine(data, "truth.json"))).Take(limit).ToList();
            Console.WriteLine($"{truths.Count} samples, {parallel} in parallel");

            if (opt.TryGetValue("dump", out var dumpIds))
            {
                var pipe = new OcrPipeline(new TesseractRunner(engine));
                foreach (var id in dumpIds.Split(','))
                {
                    var t = truths.First(x => x.id == id);
                    var o = await pipe.RunAsync(new[] { GrayImage.ReadPgm(Path.Combine(data, id + ".pgm")) }, Path.Combine(Path.GetTempPath(), "dump_" + id));
                    Console.WriteLine($"==== {id} {t.template} sev{t.severity} truth: {t.bill_no} | {t.date} | {t.amount}");
                    Console.WriteLine($"got: {o.Bill.BillNo} | {o.Bill.BillDate} | {o.Bill.BillAmount}  psm={o.PageSegMode} passes={o.PassesRun} skew={o.SkewDegrees} scale={o.ScaleFactor:0.00} conf={o.MeanWordConfidence:0}");
                    Console.WriteLine(o.Text);
                }
                return 0;
            }

            var report = new StringBuilder();
            report.AppendLine($"# OCR benchmark ({truths.Count} synthetic bills)\n");
            List<SampleResult> baseline = null, improved = null;

            if (mode is "both" or "baseline")
            {
                baseline = await RunAll(truths, parallel, async (t, ct) =>
                    (await LegacyBaseline.RunAsync(Path.Combine(data, t.id + ".png"), engine, ct), 1));
                Summarize("Baseline (original pipeline)", baseline, report);
            }
            if (mode is "both" or "improved")
            {
                var pipeline = new OcrPipeline(new TesseractRunner(engine));
                improved = await RunAll(truths, parallel, async (t, ct) =>
                {
                    var page = GrayImage.ReadPgm(Path.Combine(data, t.id + ".pgm"));
                    string work = Path.Combine(Path.GetTempPath(), "ocrbench_" + Guid.NewGuid().ToString("N"));
                    try
                    {
                        var outcome = await pipeline.RunAsync(new[] { page }, work, ct);
                        return (outcome.Bill, outcome.PassesRun);
                    }
                    finally { try { Directory.Delete(work, true); } catch { } }
                });
                Summarize("Improved pipeline", improved, report);
                WriteFailures(improved, report);
            }

            string text = report.ToString();
            Console.WriteLine(text);
            if (opt.TryGetValue("report", out var reportPath)) File.WriteAllText(reportPath, text);

            if (improved != null && minAccuracy > 0)
            {
                double acc = FieldAccuracy(improved);
                if (acc < minAccuracy)
                {
                    Console.Error.WriteLine($"Field accuracy {acc:P1} is below the required {minAccuracy:P1}");
                    return 2;
                }
            }
            return 0;
        }

        private static async Task<List<SampleResult>> RunAll(List<Truth> truths, int parallel,
            Func<Truth, CancellationToken, Task<(ParsedBill, int)>> run)
        {
            var results = new ConcurrentBag<SampleResult>();
            await Parallel.ForEachAsync(truths, new ParallelOptions { MaxDegreeOfParallelism = parallel }, async (t, ct) =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var (bill, passes) = await run(t, ct);
                    results.Add(new SampleResult { Truth = t, Bill = bill, Ms = sw.ElapsedMilliseconds, Passes = passes });
                }
                catch (Exception ex)
                {
                    results.Add(new SampleResult { Truth = t, Bill = new ParsedBill(), Ms = sw.ElapsedMilliseconds, Error = ex.Message });
                }
            });
            return results.OrderBy(r => r.Truth.id).ToList();
        }

        private static double FieldAccuracy(List<SampleResult> r) =>
            (r.Count(x => x.NoOk) + r.Count(x => x.DateOk) + r.Count(x => x.AmountOk)) / (3.0 * r.Count);

        private static void Summarize(string title, List<SampleResult> r, StringBuilder sb)
        {
            string P(int n, int d) => d == 0 ? "-" : $"{100.0 * n / d:0.0}%";
            sb.AppendLine($"## {title}\n");
            sb.AppendLine("| Subset | Bills | Bill number | Date | Amount | All three correct | Avg time |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
            void Row(string name, List<SampleResult> s) =>
                sb.AppendLine($"| {name} | {s.Count} | {P(s.Count(x => x.NoOk), s.Count)} | {P(s.Count(x => x.DateOk), s.Count)} | " +
                              $"{P(s.Count(x => x.AmountOk), s.Count)} | {P(s.Count(x => x.AllOk), s.Count)} | {(s.Count == 0 ? 0 : s.Average(x => x.Ms)):0} ms |");
            Row("**All**", r);
            string[] sev = { "clean", "mild", "moderate", "heavy" };
            for (int i = 0; i < 4; i++) Row(sev[i] + " degradation", r.Where(x => x.Truth.severity == i).ToList());
            foreach (var t in r.Select(x => x.Truth.template).Distinct().OrderBy(x => x)) Row(t, r.Where(x => x.Truth.template == t).ToList());
            sb.AppendLine($"\nField accuracy: **{100 * FieldAccuracy(r):0.0}%**. Errors: {r.Count(x => x.Error != null)}.\n");

            // Calibration: how often a field the app marks as confident (>= 0.6) is actually right.
            var fields = r.SelectMany(x => new[]
            {
                (Conf: x.Bill.BillNoConfidence, Ok: x.NoOk, Found: !string.IsNullOrEmpty(x.Bill.BillNo)),
                (Conf: x.Bill.DateConfidence, Ok: x.DateOk, Found: !string.IsNullOrEmpty(x.Bill.BillDate)),
                (Conf: x.Bill.AmountConfidence, Ok: x.AmountOk, Found: !string.IsNullOrEmpty(x.Bill.BillAmount))
            }).ToList();
            var confident = fields.Where(f => f.Found && f.Conf >= 0.6).ToList();
            var unsure = fields.Where(f => f.Found && f.Conf < 0.6).ToList();
            if (fields.Any(f => f.Conf > 0))
                sb.AppendLine($"Confidence: fields marked confident (>= 0.6) are correct {P(confident.Count(f => f.Ok), confident.Count)} of the time " +
                              $"({confident.Count} fields); fields marked unsure are correct {P(unsure.Count(f => f.Ok), unsure.Count)} ({unsure.Count} fields).\n");
        }

        private static void WriteFailures(List<SampleResult> r, StringBuilder sb)
        {
            var failed = r.Where(x => !x.AllOk).ToList();
            if (failed.Count == 0) return;
            sb.AppendLine("<details><summary>Misses</summary>\n");
            sb.AppendLine("| id | template | severity | field | expected | got |");
            sb.AppendLine("|---|---|---:|---|---|---|");
            foreach (var x in failed)
            {
                if (!x.NoOk) sb.AppendLine($"| {x.Truth.id} | {x.Truth.template} | {x.Truth.severity} | number | {x.Truth.bill_no} | {x.Bill?.BillNo} |");
                if (!x.DateOk) sb.AppendLine($"| {x.Truth.id} | {x.Truth.template} | {x.Truth.severity} | date | {x.Truth.date} | {x.Bill?.BillDate} |");
                if (!x.AmountOk) sb.AppendLine($"| {x.Truth.id} | {x.Truth.template} | {x.Truth.severity} | amount | {x.Truth.amount} | {x.Bill?.BillAmount} |");
                if (x.Error != null) sb.AppendLine($"| {x.Truth.id} | | | error | | {x.Error} |");
            }
            sb.AppendLine("\n</details>\n");
        }

        private static bool Eq(string a, string b) => !string.IsNullOrEmpty(b) && string.Equals(a?.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool AmountEq(string expected, string actual) =>
            decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) &&
            decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var e) && a == e;

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var d = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
                if (args[i].StartsWith("--")) d[args[i].Substring(2)] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
            return d;
        }
    }

    // Minimal process runner for the baseline (the improved pipeline uses TesseractRunner).
    public static class ProcessRunner
    {
        public static async Task<string> RunAsync(string exe, string arguments, int timeoutSeconds, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(exe, arguments) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            using var p = Process.Start(psi);
            var output = p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            await p.WaitForExitAsync(cts.Token);
            return await output;
        }
    }
}

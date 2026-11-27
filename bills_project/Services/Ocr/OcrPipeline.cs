using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace bills_project.Services.Ocr
{
    public sealed class OcrOutcome
    {
        public ParsedBill Bill { get; set; } = new ParsedBill();
        public int PageSegMode { get; set; }                  // the Tesseract mode whose result was used
        public int PassesRun { get; set; }
        public double MeanWordConfidence { get; set; }        // 0-100 over the chosen pass
        public double SkewDegrees { get; set; }
        public double ScaleFactor { get; set; }
        public long ElapsedMs { get; set; }
        public string Text { get; set; } = "";
    }

    public sealed class OcrPipelineOptions
    {
        public bool Preprocess { get; set; } = true;
        // Tried in order; later modes only run while the result is still incomplete or low-confidence.
        public int[] PageSegModes { get; set; } = { 6, 4, 11 };
    }

    // The full OCR pipeline for one document (one or more page images):
    // preprocess each page -> Tesseract (TSV with word confidences) -> regroup words into rows -> parse fields.
    // If the first page segmentation mode doesn't give a confident result, other modes are tried and the
    // best-scoring result is kept.
    public sealed class OcrPipeline
    {
        private readonly TesseractRunner _tesseract;
        private readonly OcrPipelineOptions _options;

        public OcrPipeline(TesseractRunner tesseract, OcrPipelineOptions options = null)
        {
            _tesseract = tesseract ?? throw new ArgumentNullException(nameof(tesseract));
            _options = options ?? new OcrPipelineOptions();
        }

        public async Task<OcrOutcome> RunAsync(IReadOnlyList<GrayImage> pages, string workDir, CancellationToken ct = default)
        {
            if (pages == null || pages.Count == 0) throw new ArgumentException("No pages to read.", nameof(pages));
            var timer = Stopwatch.StartNew();
            Directory.CreateDirectory(workDir);

            // 1. preprocess and save each page once
            var pagePaths = new List<string>();
            double skew = 0, scale = 1;
            for (int p = 0; p < pages.Count; p++)
            {
                var image = pages[p];
                if (_options.Preprocess)
                {
                    var prepared = ImagePreprocessor.Process(image);
                    image = prepared.Image;
                    if (p == 0) { skew = prepared.SkewDegrees; scale = prepared.ScaleFactor; }
                }
                string path = Path.Combine(workDir, $"ocr-page-{p + 1}.pgm");
                image.WritePgm(path);
                pagePaths.Add(path);
            }

            // 2. recognise with each page segmentation mode until the result is confident
            OcrOutcome best = null;
            int passes = 0;
            foreach (int psm in _options.PageSegModes)
            {
                ct.ThrowIfCancellationRequested();
                var lines = new List<OcrLine>();
                foreach (var path in pagePaths)
                {
                    string tsv = await _tesseract.RecognizeTsvAsync(path, psm, ct).ConfigureAwait(false);
                    lines.AddRange(TesseractTsv.Parse(tsv));
                }
                passes++;

                var bill = BillTextParser.Parse(lines);
                var words = lines.SelectMany(l => l.Words).ToList();
                var outcome = new OcrOutcome
                {
                    Bill = bill,
                    PageSegMode = psm,
                    MeanWordConfidence = words.Count == 0 ? 0 : words.Average(w => w.Confidence),
                    Text = string.Join("\n", lines.Select(l => l.Text))
                };
                if (best == null || Rank(outcome) > Rank(best)) best = outcome;
                if (best.Bill.Confidence == "high") break;
            }

            best.PassesRun = passes;
            best.SkewDegrees = skew;
            best.ScaleFactor = scale;
            best.ElapsedMs = timer.ElapsedMilliseconds;
            return best;
        }

        // More fields first, then summed field confidence, then Tesseract's overall confidence.
        private static double Rank(OcrOutcome o) => o.Bill.FieldsFound * 10 + o.Bill.Score + o.MeanWordConfidence / 1000.0;
    }
}

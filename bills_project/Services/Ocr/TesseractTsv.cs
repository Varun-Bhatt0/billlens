using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace bills_project.Services.Ocr
{
    public sealed class OcrWord
    {
        public string Text { get; set; }
        public double Confidence { get; set; }   // 0-100, as reported by Tesseract
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public sealed class OcrLine
    {
        public List<OcrWord> Words { get; } = new List<OcrWord>();
        public string Text => string.Join(" ", Words.Select(w => w.Text));
        public double MeanConfidence => Words.Count == 0 ? 0 : Words.Average(w => w.Confidence);
        public int Top => Words.Count == 0 ? 0 : Words.Min(w => w.Top);
        public int Bottom => Words.Count == 0 ? 0 : Words.Max(w => w.Top + w.Height);
        public double CenterY => (Top + Bottom) / 2.0;
        public int Left => Words.Count == 0 ? 0 : Words.Min(w => w.Left);

        public static OcrLine FromText(string text)
        {
            var line = new OcrLine();
            foreach (var token in (text ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                line.Words.Add(new OcrWord { Text = token, Confidence = 100 });
            return line;
        }
    }

    // Parses Tesseract's TSV output (one row per word, with position and confidence) into lines.
    // Words are regrouped into visual rows by vertical position, so a label on the left and its value
    // far to the right ("Grand Total ........ 1,240.50") end up on the same line even when Tesseract
    // treats them as separate blocks.
    public static class TesseractTsv
    {
        public static List<OcrWord> ParseWords(string tsv)
        {
            var words = new List<OcrWord>();
            if (string.IsNullOrEmpty(tsv)) return words;
            foreach (var raw in tsv.Split('\n'))
            {
                var cols = raw.TrimEnd('\r').Split('\t');
                // level page block par line word left top width height conf text
                if (cols.Length < 12 || cols[0] != "5") continue;
                string text = cols[11].Trim();
                if (text.Length == 0) continue;
                if (!double.TryParse(cols[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double conf) || conf < 0) continue;
                words.Add(new OcrWord
                {
                    Text = text,
                    Confidence = conf,
                    Left = int.Parse(cols[6], CultureInfo.InvariantCulture),
                    Top = int.Parse(cols[7], CultureInfo.InvariantCulture),
                    Width = int.Parse(cols[8], CultureInfo.InvariantCulture),
                    Height = int.Parse(cols[9], CultureInfo.InvariantCulture)
                });
            }
            return words;
        }

        public static List<OcrLine> GroupIntoLines(IEnumerable<OcrWord> words)
        {
            var lines = new List<OcrLine>();
            foreach (var word in words.OrderBy(w => w.Top + w.Height / 2.0))
            {
                double center = word.Top + word.Height / 2.0;
                // join the line whose vertical centre is within half a word height
                var line = lines.LastOrDefault(l => Math.Abs(l.CenterY - center) <= Math.Max(6, word.Height * 0.55));
                if (line == null) { line = new OcrLine(); lines.Add(line); }
                line.Words.Add(word);
            }
            foreach (var line in lines)
                line.Words.Sort((a, b) => a.Left.CompareTo(b.Left));
            return lines.OrderBy(l => l.Top).ToList();
        }

        public static List<OcrLine> Parse(string tsv) => GroupIntoLines(ParseWords(tsv));
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using bills_project.Services.Ocr;

namespace bills_project.Services
{
    public sealed class ParsedBill
    {
        public string BillNo { get; set; } = "";
        public string BillDate { get; set; } = "";     // yyyy-MM-dd
        public string BillAmount { get; set; } = "";   // plain decimal, e.g. "1240.5"

        // 0-1 per field: label strength, validation checks and Tesseract's own word confidence combined.
        public double BillNoConfidence { get; set; }
        public double DateConfidence { get; set; }
        public double AmountConfidence { get; set; }

        // True when sub-total + tax add up to the extracted total (an independent check on the amount).
        public bool AmountCrossChecked { get; set; }

        public int FieldsFound => new[] { BillNo, BillDate, BillAmount }.Count(v => !string.IsNullOrEmpty(v));
        public double Score => BillNoConfidence + DateConfidence + AmountConfidence;

        public string Confidence
        {
            get
            {
                double weakest = Math.Min(BillNoConfidence, Math.Min(DateConfidence, AmountConfidence));
                if (FieldsFound == 3 && weakest >= 0.6) return "high";
                if (FieldsFound >= 2) return "medium";
                return "low";
            }
        }
    }

    // Extracts the bill number, bill date and total amount from OCR output.
    //
    // Works line by line on Tesseract's words (with their confidences) and is built to survive typical
    // OCR damage: misread label letters ("T0tal", "lnvoice", "Arnount"), letters inside numbers
    // ("1O4O.5O"), labels and values split across a wide gap, and distractors such as due dates,
    // sub-totals, tax lines and item prices. Each field is chosen from scored candidates rather than
    // the first regex match.
    public static class BillTextParser
    {
        // ------------------------------------------------------------------ public API

        public static ParsedBill Parse(string text)
        {
            var lines = (text ?? "").Replace("\r", "").Split('\n')
                .Where(l => l.Trim().Length > 0)
                .Select(OcrLine.FromText)
                .ToList();
            return Parse(lines);
        }

        public static ParsedBill Parse(IReadOnlyList<OcrLine> lines)
        {
            var result = new ParsedBill();
            if (lines == null || lines.Count == 0) return result;
            var prepared = lines.Select((l, i) => new Line(l, i, lines.Count)).ToList();

            var number = FindBillNumber(prepared);
            if (number != null) { result.BillNo = number.Value; result.BillNoConfidence = number.Confidence; }

            var date = FindDate(prepared);
            if (date != null) { result.BillDate = date.Value; result.DateConfidence = date.Confidence; }

            var amount = FindAmount(prepared, out bool crossChecked);
            if (amount != null) { result.BillAmount = amount.Value; result.AmountConfidence = amount.Confidence; result.AmountCrossChecked = crossChecked; }

            return result;
        }

        // ------------------------------------------------------------------ line model

        private sealed class Line
        {
            public readonly OcrLine Source;
            public readonly int Index;
            public readonly double Position;   // 0 = top of page, 1 = bottom
            public readonly string[] Raw;      // tokens as read
            public readonly string[] Label;    // tokens normalised for label matching
            public readonly string LabelText;

            public Line(OcrLine source, int index, int count)
            {
                Source = source;
                Index = index;
                Position = count <= 1 ? 0 : (double)index / (count - 1);
                Raw = source.Words.Select(w => w.Text).ToArray();
                Label = Raw.Select(NormalizeLabelToken).ToArray();
                LabelText = " " + string.Join(" ", Label) + " ";
            }

            public double WordConfidence(int index) =>
                index >= 0 && index < Source.Words.Count ? Source.Words[index].Confidence / 100.0 : 0.5;
        }

        private sealed class Candidate
        {
            public string Value;
            public double Score;
            public double Confidence;
            public int LineIndex;
        }

        // Lower-cases a token and undoes common OCR letter/digit swaps inside words, so labels match.
        internal static string NormalizeLabelToken(string token)
        {
            var t = token.ToLowerInvariant().Trim(':', '.', ',', ';', '-', '_', '|', '#', '*');
            int letters = t.Count(char.IsLetter), digits = t.Count(char.IsDigit);
            if (letters >= 2 && letters >= digits)
            {
                var sb = new StringBuilder(t.Length);
                foreach (char c in t)
                    sb.Append(c == '0' ? 'o' : c == '1' || c == '|' || c == '!' ? 'l' : c == '5' ? 's' : c == '8' ? 'b' : c);
                t = sb.ToString().Replace("rn", "m").Replace("vv", "w");
                if (t.StartsWith("l") && t.Length > 3 && (t.StartsWith("lnv") || t.StartsWith("ld"))) t = "i" + t.Substring(1);
            }
            return t;
        }

        // True if `word` is `keyword` allowing OCR errors: one for words of 5-6 letters, two for longer ones.
        private static bool Like(string word, string keyword)
        {
            if (word == keyword) return true;
            if (keyword.Length < 5) return false;
            int allowed = keyword.Length >= 7 ? 2 : 1;
            if (Math.Abs(word.Length - keyword.Length) > allowed) return false;
            return Levenshtein(word, keyword) <= allowed;
        }

        private static int Levenshtein(string a, string b)
        {
            var d = new int[a.Length + 1, b.Length + 1];
            for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= b.Length; j++) d[0, j] = j;
            for (int i = 1; i <= a.Length; i++)
                for (int j = 1; j <= b.Length; j++)
                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            return d[a.Length, b.Length];
        }

        private static int FindWord(Line line, params string[] keywords)
        {
            for (int i = 0; i < line.Label.Length; i++)
                foreach (var k in keywords)
                    if (Like(line.Label[i], k)) return i;
            return -1;
        }

        // ------------------------------------------------------------------ bill number

        private static readonly (string Word, double Weight)[] NumberSubjects =
        {
            ("invoice", 1.0), ("inv", 0.95), ("bill", 0.95), ("receipt", 0.95), ("voucher", 0.9),
            ("txn", 0.8), ("transaction", 0.8), ("reference", 0.7), ("ref", 0.7), ("document", 0.6), ("order", 0.6)
        };
        private static readonly string[] NumberMarkers = { "no", "number", "num", "nr", "#", "id", "n0" };
        private static readonly string[] NumberExclusions = { "gstin", "gst", "consumer", "customer", "mobile", "phone", "tel", "pan", "account", "card", "meter", "patient", "table" };
        private static readonly Regex BillNoToken = new Regex(@"^[A-Za-z0-9][A-Za-z0-9/\-]{1,29}$", RegexOptions.CultureInvariant);

        private static Candidate FindBillNumber(List<Line> lines)
        {
            var candidates = new List<Candidate>();
            foreach (var line in lines)
            {
                for (int i = 0; i < line.Label.Length; i++)
                {
                    var subject = NumberSubjects.FirstOrDefault(s => Like(line.Label[i], s.Word) || line.Label[i].StartsWith(s.Word + "#") || line.Label[i] == s.Word + "no");
                    if (subject.Word == null) continue;

                    // the word before the subject mustn't be an exclusion ("Consumer Bill No" is fine, "Account No" is not)
                    if (i > 0 && NumberExclusions.Any(x => Like(line.Label[i - 1], x)) && subject.Word != "bill") continue;

                    int j = i + 1;
                    bool marker = line.Raw[i].Contains("#") || line.Label[i].EndsWith("no");
                    while (j < line.Raw.Length && (NumberMarkers.Contains(line.Label[j]) || line.Raw[j].Trim(':', '.', '#', '-').Length == 0))
                    {
                        if (NumberMarkers.Contains(line.Label[j]) || line.Raw[j].Contains("#")) marker = true;
                        j++;
                    }
                    // value glued to the label: "Invoice#95201", "No:5521"
                    string value = null; int valueIndex = j;
                    var glued = Regex.Match(line.Raw[i], @"[#:]\s*([A-Za-z0-9][A-Za-z0-9/\-]+)$");
                    if (glued.Success && glued.Groups[1].Value.Any(char.IsDigit)) { value = glued.Groups[1].Value; valueIndex = i; marker = true; }
                    else if (j < line.Raw.Length) value = line.Raw[j];
                    if (value == null) continue;

                    value = CleanBillNumber(value);
                    if (value == null) continue;
                    if (!marker && !(subject.Word == "invoice" || subject.Word == "receipt")) continue;

                    double score = subject.Weight + (marker ? 0.3 : 0) + (1 - line.Position) * 0.2;
                    double conf = Math.Min(1.0, 0.5 * subject.Weight + 0.15 + 0.35 * line.WordConfidence(valueIndex));
                    candidates.Add(new Candidate { Value = value, Score = score, Confidence = conf, LineIndex = line.Index });
                }
            }
            return candidates.OrderByDescending(c => c.Score).FirstOrDefault();
        }

        private static string CleanBillNumber(string raw)
        {
            string v = raw.Trim().Trim(':', '.', ',', ';', '|', '#', ')', '(', '[', ']');
            if (v.Length < 2 || !BillNoToken.IsMatch(v) || !v.Any(char.IsDigit)) return null;
            if (Regex.IsMatch(v, @"^\d{1,2}[/\-.]\d{1,2}[/\-.]\d{2,4}$")) return null; // that's a date
            // inside a run of digits, letters that look like digits are almost always misreads
            v = Regex.Replace(v, @"(?<=\d)[Oo](?=\d|$)|(?<=^|\d)[Oo](?=\d)", "0");
            v = Regex.Replace(v, @"(?<=\d)[lI|](?=\d|$)|(?<=^|\d)[lI|](?=\d)", "1");
            return v;
        }

        // ------------------------------------------------------------------ date

        private static readonly Dictionary<string, int> Months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            {"jan",1},{"feb",2},{"mar",3},{"apr",4},{"may",5},{"jun",6},{"jul",7},{"aug",8},{"sep",9},{"sept",9},{"oct",10},{"nov",11},{"dec",12},
            {"january",1},{"february",2},{"march",3},{"april",4},{"june",6},{"july",7},{"august",8},{"september",9},{"october",10},{"november",11},{"december",12}
        };

        private const string D = @"[0-9OolI|SB]";
        private static readonly Regex NumericDate = new Regex(
            $@"(?<!\d)({D}{{1,2}})\s?[/\-.]\s?({D}{{1,2}})\s?[/\-.]\s?({D}{{4}}|{D}{{2}})(?!\d)", RegexOptions.CultureInvariant);
        private static readonly Regex IsoDate = new Regex(@"(?<!\d)(\d{4})-(\d{1,2})-(\d{1,2})(?!\d)", RegexOptions.CultureInvariant);
        private static readonly Regex DayMonthName = new Regex(
            @"(?<!\d)(\d{1,2})(?:st|nd|rd|th)?[\s\-./]*([A-Za-z]{3,9})[\s\-.,/]*(\d{4}|\d{2})(?!\d)", RegexOptions.CultureInvariant);
        private static readonly Regex MonthNameDay = new Regex(
            @"\b([A-Za-z]{3,9})\.?\s+(\d{1,2})(?:st|nd|rd|th)?,?\s+(\d{4})(?!\d)", RegexOptions.CultureInvariant);

        private static readonly string[] PositiveDateLabels = { "invoice", "bill", "date", "dated", "receipt", "issued", "inv" };
        private static readonly string[] NegativeDateLabels = { "due", "printed", "print", "expiry", "exp", "valid", "till", "until", "period", "from", "next", "last", "previous", "reading", "dob", "birth", "delivery" };

        private static Candidate FindDate(List<Line> lines)
        {
            var candidates = new List<Candidate>();
            foreach (var line in lines)
            {
                string text = string.Join(" ", line.Raw);
                foreach (var (iso, quality) in DatesIn(text))
                {
                    if (!IsPlausible(iso)) continue;
                    bool positive = PositiveDateLabels.Any(k => line.Label.Any(w => Like(w, k)));
                    bool negative = NegativeDateLabels.Any(k => line.Label.Any(w => Like(w, k)));
                    bool billDateLabel = FindWord(line, "invoice", "bill", "receipt") >= 0 && FindWord(line, "date", "dated") >= 0;

                    double score = quality + (positive ? 0.6 : 0) + (billDateLabel ? 0.4 : 0) - (negative ? 1.2 : 0) + (1 - line.Position) * 0.3;
                    double conf = Math.Max(0.1, Math.Min(1.0, 0.3 + (positive ? 0.25 : 0) + (negative ? -0.3 : 0) + 0.15 * quality + 0.3 * line.Source.MeanConfidence / 100));
                    candidates.Add(new Candidate { Value = iso, Score = score, Confidence = conf, LineIndex = line.Index });
                }
            }
            return candidates.OrderByDescending(c => c.Score).FirstOrDefault();
        }

        // All dates in a line as yyyy-MM-dd, with a quality score (unambiguous formats score higher).
        private static IEnumerable<(string Iso, double Quality)> DatesIn(string text)
        {
            foreach (Match m in IsoDate.Matches(text))
                if (TryDate(Int(m.Groups[1].Value), Int(m.Groups[2].Value), Int(m.Groups[3].Value), out var iso)) yield return (iso, 1.0);

            foreach (Match m in DayMonthName.Matches(text))
                if (Months.TryGetValue(m.Groups[2].Value, out int month) && TryDate(Year(m.Groups[3].Value), month, Int(m.Groups[1].Value), out var iso))
                    yield return (iso, 1.0);

            foreach (Match m in MonthNameDay.Matches(text))
                if (Months.TryGetValue(m.Groups[1].Value, out int month) && TryDate(Year(m.Groups[3].Value), month, Int(m.Groups[2].Value), out var iso))
                    yield return (iso, 1.0);

            foreach (Match m in NumericDate.Matches(text))
            {
                string a = FixDigits(m.Groups[1].Value), b = FixDigits(m.Groups[2].Value), c = FixDigits(m.Groups[3].Value);
                bool fixedChars = a != m.Groups[1].Value || b != m.Groups[2].Value || c != m.Groups[3].Value;
                // Indian bills are day-first; fall back to month-first only when day-first is impossible.
                if (TryDate(Year(c), Int(b), Int(a), out var iso)) yield return (iso, fixedChars ? 0.6 : 0.9);
                else if (TryDate(Year(c), Int(a), Int(b), out iso)) yield return (iso, 0.5);
            }
        }

        private static string FixDigits(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
                sb.Append(ch == 'O' || ch == 'o' ? '0' : ch == 'l' || ch == 'I' || ch == '|' ? '1' : ch == 'S' ? '5' : ch == 'B' ? '8' : ch);
            return sb.ToString();
        }

        private static int Int(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : -1;
        private static int Year(string s) { int y = Int(s); return y >= 0 && y < 100 ? 2000 + y : y; }

        private static bool TryDate(int year, int month, int day, out string iso)
        {
            iso = null;
            if (year < 1990 || year > 2100 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
            iso = new DateTime(year, month, day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        private static bool IsPlausible(string iso)
        {
            var d = DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            return d >= new DateTime(2000, 1, 1) && d <= DateTime.Today.AddDays(31);
        }

        // ------------------------------------------------------------------ amount

        private static readonly (string[] Words, double Weight)[] TotalLabels =
        {
            (new[] { "grand", "total" }, 1.0),
            (new[] { "net", "payable" }, 0.95),
            (new[] { "amount", "payable" }, 0.95),
            (new[] { "total", "due" }, 0.9),
            (new[] { "amount", "due" }, 0.85),
            (new[] { "net", "amount" }, 0.85),
            (new[] { "total", "amount" }, 0.85),
            (new[] { "bill", "amount" }, 0.8),
            (new[] { "total" }, 0.7),
            (new[] { "payable" }, 0.7),
            (new[] { "amount" }, 0.4),
        };
        private static readonly string[] NotTotal = { "sub", "subtotal", "tax", "gst", "cgst", "sgst", "igst", "vat", "discount", "round", "rounding", "saved", "savings", "paid", "change", "tendered", "cash", "units", "qty", "items", "previous", "arrears" };

        private static readonly Regex Money = new Regex(
            @"(?<![\w.,])(?:Rs\.?|INR|₹|\?)?\s?((?:\d{1,3}(?:[,]\d{2,3})+|\d+)(?:[.,]\d{1,2})?)(?![\w%]|[.,]\d)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static Candidate FindAmount(List<Line> lines, out bool crossChecked)
        {
            crossChecked = false;
            var candidates = new List<Candidate>();
            var allAmounts = new List<(AmountToken Token, Line Line)>();
            double? subtotal = null, tax = 0;

            foreach (var line in lines)
            {
                var amounts = AmountsIn(line).ToList();
                allAmounts.AddRange(amounts.Select(a => (a, line)));
                bool isSub = FindWord(line, "subtotal") >= 0 || (FindWord(line, "sub") >= 0 && FindWord(line, "total") >= 0);
                bool isTax = FindWord(line, "tax", "gst", "cgst", "sgst", "igst", "vat") >= 0;
                if (isSub && amounts.Count > 0) subtotal = amounts.Last().Value;
                else if (isTax && amounts.Count > 0 && FindWord(line, "total") < 0) tax += amounts.Last().Value;

                var label = TotalLabels.FirstOrDefault(l => l.Words.All(w => FindWord(line, w) >= 0));
                if (label.Words == null) continue;
                if (NotTotal.Any(n => FindWord(line, n) >= 0) && !(label.Words.Contains("grand") || label.Words.Contains("payable"))) continue;

                // value on the same line, or alone on the next line when the columns were split
                var values = amounts;
                double layoutPenalty = 0;
                if (values.Count == 0 && line.Index + 1 < lines.Count)
                {
                    var next = lines[line.Index + 1];
                    if (next.Label.All(t => !t.Any(char.IsLetter) || t == "rs" || t == "inr")) { values = AmountsIn(next).ToList(); layoutPenalty = 0.1; }
                }
                if (values.Count == 0) continue;

                var v = values.Last(); // the rightmost number on a total line is the total
                double score = label.Weight + line.Position * 0.3 - layoutPenalty + (v.HasCurrency ? 0.1 : 0) + (v.HasDecimals ? 0.1 : 0);
                double conf = Math.Min(1.0, 0.45 * label.Weight + 0.2 + 0.35 * v.WordConfidence - layoutPenalty);
                candidates.Add(new Candidate { Value = Format(v.Value), Score = score, Confidence = conf, LineIndex = line.Index });
            }

            decimal? expected = subtotal.HasValue && tax.Value > 0 ? (decimal)subtotal.Value + (decimal)tax.Value : (decimal?)null;

            if (candidates.Count == 0)
            {
                // No readable total label (badly damaged text). If sub-total + tax is printed somewhere, use it;
                // otherwise take the largest amount with paise in the lower half of the page. Low confidence either way.
                var fallback = expected.HasValue
                    ? allAmounts.FirstOrDefault(a => Math.Abs((decimal)a.Token.Value - expected.Value) <= 0.05m)
                    : default;
                if (fallback.Token != null)
                {
                    crossChecked = true;
                    return new Candidate { Value = Format(fallback.Token.Value), Score = 0.5, Confidence = 0.6, LineIndex = fallback.Line.Index };
                }
                var largest = allAmounts.Where(a => a.Token.HasDecimals && a.Line.Position >= 0.5).OrderByDescending(a => a.Token.Value).FirstOrDefault();
                if (largest.Token == null) return null;
                return new Candidate { Value = Format(largest.Token.Value), Score = 0.2, Confidence = 0.3, LineIndex = largest.Line.Index };
            }

            // Arithmetic check: a total equal to sub-total + tax is almost certainly right.
            if (expected.HasValue)
            {
                foreach (var c in candidates)
                {
                    decimal value = decimal.Parse(c.Value, CultureInfo.InvariantCulture);
                    if (Math.Abs(value - expected.Value) <= 0.05m)
                    {
                        c.Score += 1.0;
                        c.Confidence = Math.Min(1.0, c.Confidence + 0.3);
                    }
                    else if (DropLeadingDigit(value) is decimal repaired && Math.Abs(repaired - expected.Value) <= 0.05m)
                    {
                        // A currency sign read as a digit ("₹28,812.86" -> "728,812.86"): the arithmetic says which digit is extra.
                        c.Value = Format((double)repaired);
                        c.Score += 0.8;
                        c.Confidence = Math.Min(1.0, c.Confidence + 0.15);
                    }
                }
            }

            var best = candidates.OrderByDescending(c => c.Score).First();
            decimal bestValue = decimal.Parse(best.Value, CultureInfo.InvariantCulture);
            crossChecked = expected.HasValue && Math.Abs(bestValue - expected.Value) <= 0.05m;

            if (!crossChecked)
            {
                // Magnitude check: a total many times larger than every other amount on the bill usually has a
                // currency sign misread as a leading digit. Repair it if the result is still the largest amount.
                var others = allAmounts.Where(a => a.Line.Index != best.LineIndex && a.Token.HasDecimals).Select(a => (decimal)a.Token.Value).ToList();
                if (others.Count >= 2)
                {
                    decimal secondLargest = others.Max();
                    if (bestValue > 3 * secondLargest && DropLeadingDigit(bestValue) is decimal repaired && repaired >= secondLargest * 0.95m)
                    {
                        best.Value = Format((double)repaired);
                        best.Confidence = Math.Min(best.Confidence, 0.5);
                    }
                }
            }
            return best;
        }

        private static decimal? DropLeadingDigit(decimal value)
        {
            string s = value.ToString("0.##", CultureInfo.InvariantCulture);
            if (s.Length < 3 || !char.IsDigit(s[0])) return null;
            return decimal.TryParse(s.Substring(1), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : (decimal?)null;
        }

        private sealed class AmountToken
        {
            public double Value;
            public bool HasCurrency;
            public bool HasDecimals;
            public double WordConfidence;
        }

        private static IEnumerable<AmountToken> AmountsIn(Line line)
        {
            for (int i = 0; i < line.Raw.Length; i++)
            {
                // fix letters inside numeric tokens: "1,24O.5O" -> "1,240.50"
                string token = line.Raw[i];
                bool currency = Regex.IsMatch(token, @"^(Rs\.?|INR|₹)", RegexOptions.IgnoreCase) || (i > 0 && Regex.IsMatch(line.Raw[i - 1], @"^(Rs\.?|INR|₹)$", RegexOptions.IgnoreCase));
                string cleaned = Regex.Replace(token, @"^(Rs\.?|INR|₹|\?|[^\d]{1,2}(?=\d))", "", RegexOptions.IgnoreCase);
                // a token made only of digits, separators and look-alike letters is a number with misread digits
                if (cleaned.Count(char.IsDigit) >= 2 && Regex.IsMatch(cleaned, @"^[\dOolI|,.]+$"))
                    cleaned = cleaned.Replace('O', '0').Replace('o', '0').Replace('l', '1').Replace('I', '1').Replace('|', '1');
                var m = Money.Match(cleaned);
                if (!m.Success || m.Index > 2) continue;
                string number = m.Groups[1].Value;
                if (number.Length >= 2 && number[number.Length - 3 < 0 ? 0 : number.Length - 3] == ',' && number.IndexOf('.') < 0)
                    number = number.Substring(0, number.Length - 3) + "." + number.Substring(number.Length - 2); // "1240,50" decimal comma
                string normalized = BillFormat.NormalizeAmount(number);
                if (normalized.Length == 0) continue;
                double value = double.Parse(normalized, CultureInfo.InvariantCulture);
                if (value <= 0 || value > 100000000) continue;
                yield return new AmountToken
                {
                    Value = value,
                    HasCurrency = currency,
                    HasDecimals = number.Contains("."),
                    WordConfidence = line.WordConfidence(i)
                };
            }
        }

        private static string Format(double value) => ((decimal)value).ToString("0.##", CultureInfo.InvariantCulture);
    }
}

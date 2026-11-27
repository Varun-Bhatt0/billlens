using System.Data;
using System.Linq;
using bills_project.Services;
using bills_project.Services.Ocr;
using Xunit;

namespace BillsProject.Tests
{
    public class TesseractTsvTests
    {
        private const string Header = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n";

        private static string Word(int block, int left, int top, int width, int height, double conf, string text) =>
            $"5\t1\t{block}\t1\t1\t1\t{left}\t{top}\t{width}\t{height}\t{conf}\t{text}\n";

        [Fact]
        public void ParseWords_ReadsWordRowsAndSkipsStructureRows()
        {
            string tsv = Header +
                         "1\t1\t0\t0\t0\t0\t0\t0\t800\t600\t-1\t\n" +   // page row
                         Word(1, 10, 10, 60, 20, 96.5, "Invoice") +
                         Word(1, 80, 10, 30, 20, 91, "No:") +
                         "5\t1\t1\t1\t1\t4\t120\t10\t5\t20\t95\t \n";   // empty word
            var words = TesseractTsv.ParseWords(tsv);
            Assert.Equal(new[] { "Invoice", "No:" }, words.Select(w => w.Text));
            Assert.Equal(96.5, words[0].Confidence);
        }

        [Fact]
        public void GroupIntoLines_JoinsALabelWithAValueFarAwayInAnotherBlock()
        {
            // Tesseract put the amount in a separate block on the right; same row on the page.
            string tsv = Header +
                         Word(1, 20, 300, 80, 22, 93, "Grand") +
                         Word(1, 110, 301, 70, 22, 92, "Total") +
                         Word(7, 900, 303, 120, 22, 90, "1,240.50") +
                         Word(1, 20, 340, 80, 22, 93, "Thank");
            var lines = TesseractTsv.Parse(tsv);
            Assert.Equal(2, lines.Count);
            Assert.Equal("Grand Total 1,240.50", lines[0].Text);
            Assert.Equal("1240.5", BillTextParser.Parse(lines).BillAmount);
        }

        [Fact]
        public void GroupIntoLines_OrdersWordsLeftToRight()
        {
            string tsv = Header + Word(1, 300, 10, 40, 20, 90, "B") + Word(1, 10, 12, 40, 20, 90, "A");
            Assert.Equal("A B", TesseractTsv.Parse(tsv).Single().Text);
        }

        [Fact]
        public void BillSummary_AddsUpAmountsAndFindsLatestDate()
        {
            var dt = new DataTable();
            dt.Columns.Add("BillAmount", typeof(string));
            dt.Columns.Add("SubmittedOn", typeof(string));
            dt.Rows.Add("100000.50", "2026-01-05 10:00:00");
            dt.Rows.Add("20000", "2026-03-01 09:30:00");
            var s = BillSummary.From(dt);
            Assert.Equal(2, s.Count);
            Assert.Equal(120000.50m, s.Total);
            Assert.Equal("1,20,000.50", s.TotalText);
            Assert.Equal("1 Mar 2026", s.LastSubmittedText);
        }
    }
}

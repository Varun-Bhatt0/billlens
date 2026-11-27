using System.Collections.Generic;
using System.Linq;
using bills_project.Services;
using bills_project.Services.Ocr;
using Xunit;

namespace BillsProject.Tests
{
    public class BillTextParserTests
    {
        [Fact]
        public void Parse_FindsAllThreeFieldsOnATypicalInvoice()
        {
            const string text = @"ACME MEDICAL STORE
Invoice No: INV-2024/0193
Invoice Date: 14/08/2024
Paracetamol 500mg x2      40.00
Sub Total                 1,051.27
GST @18%                    189.23
Grand Total: Rs. 1,240.50
Thank you";

            var bill = BillTextParser.Parse(text);

            Assert.Equal("INV-2024/0193", bill.BillNo);
            Assert.Equal("2024-08-14", bill.BillDate);
            Assert.Equal("1240.5", bill.BillAmount);
            Assert.True(bill.AmountCrossChecked);
            Assert.Equal("high", bill.Confidence);
        }

        [Fact]
        public void Parse_PrefersTotalOverSubtotalTaxAndItems()
        {
            const string text = "Bill No. 5521\nDate 01-02-2025\nAmount 100.00\nSub Total 100.00\nTax 18.00\nTotal Amount: 118.00";
            Assert.Equal("118", BillTextParser.Parse(text).BillAmount);
        }

        [Fact]
        public void Parse_HandlesIndianDigitGroupingAndCurrencySymbols()
        {
            var bill = BillTextParser.Parse("Receipt No: R77-AB1\nNet Amount: ₹1,20,000.00");
            Assert.Equal("120000", bill.BillAmount);
        }

        [Fact]
        public void Parse_IgnoresDueDateAndPrintedDate()
        {
            const string text = "Bill No: 88213\nBill Date: 03/12/2025\nDue Date: 18/12/2025\nAmount Payable: 1,240.50\nPrinted on 05/12/2025";
            Assert.Equal("2025-12-03", BillTextParser.Parse(text).BillDate);
        }

        [Theory]
        [InlineData("Invoice Date: 12 Aug 2024", "2024-08-12")]
        [InlineData("Dated: Aug 12, 2024", "2024-08-12")]
        [InlineData("Date: 12-Aug-2024", "2024-08-12")]
        [InlineData("Date: 2024-08-12", "2024-08-12")]
        [InlineData("Date: 12.08.24", "2024-08-12")]
        public void Parse_ReadsMonthNamesAndShortYears(string line, string expected)
        {
            Assert.Equal(expected, BillTextParser.Parse(line).BillDate);
        }

        [Fact]
        public void Parse_ToleratesTypicalOcrMisreadsInLabelsAndNumbers()
        {
            // "lnvoice", "N0", "T0tal", "Arnount" and O-for-0 inside numbers are all common Tesseract errors
            const string text = "lnvoice N0: 4471\nDate: l4/O8/2024\nSub T0tal 1,000.00\nGST 180.00\nT0tal Arnount: 1,18O.OO";
            var bill = BillTextParser.Parse(text);
            Assert.Equal("4471", bill.BillNo);
            Assert.Equal("2024-08-14", bill.BillDate);
            Assert.Equal("1180", bill.BillAmount);
        }

        [Fact]
        public void Parse_RepairsCurrencySignReadAsDigit_UsingTheArithmetic()
        {
            // "₹ 28,812.86" read as "728,812.86"; sub-total + tax proves which digit is extra
            const string text = "Bill No: 11\nDate: 01/01/2025\nSub Total 24,417.68\nGST @18% 4,395.18\nGrand Total 728,812.86";
            var bill = BillTextParser.Parse(text);
            Assert.Equal("28812.86", bill.BillAmount);
            Assert.True(bill.AmountCrossChecked);
        }

        [Fact]
        public void Parse_DoesNotTreatBillToOrAccountNumbersAsTheBillNumber()
        {
            var bill = BillTextParser.Parse("Bill To: City Stores\nAccount No: 99887766\nConsumer No: 1234\nTotal 50");
            Assert.Equal("", bill.BillNo);
        }

        [Fact]
        public void Parse_UsesWordConfidenceFromTesseract()
        {
            OcrLine Line(params (string Text, double Conf)[] words)
            {
                var l = new OcrLine();
                foreach (var (t, c) in words) l.Words.Add(new OcrWord { Text = t, Confidence = c });
                return l;
            }
            var sure = BillTextParser.Parse(new List<OcrLine> { Line(("Invoice", 95), ("No:", 95), ("A-100", 96)) });
            var unsure = BillTextParser.Parse(new List<OcrLine> { Line(("Invoice", 95), ("No:", 95), ("A-100", 20)) });
            Assert.Equal("A-100", unsure.BillNo);
            Assert.True(sure.BillNoConfidence > unsure.BillNoConfidence);
        }

        [Fact]
        public void Parse_EmptyOrNullTextReturnsNothing()
        {
            Assert.Equal(0, BillTextParser.Parse((string)null).FieldsFound);
            Assert.Equal("low", BillTextParser.Parse("").Confidence);
        }
    }
}

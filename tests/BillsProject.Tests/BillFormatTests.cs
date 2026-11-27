using bills_project.Services;
using Xunit;

namespace BillsProject.Tests
{
    public class BillFormatTests
    {
        [Theory]
        [InlineData("25/12/2024", "2024-12-25")]
        [InlineData("5-3-24", "2024-03-05")]      // day first, two-digit year
        [InlineData("31.01.2025", "2025-01-31")]
        [InlineData("2024-01-31", "2024-01-31")]  // already ISO (what <input type="date"> sends)
        [InlineData(" 01/02/2024 ", "2024-02-01")]
        public void NormalizeDate_ReadsCommonIndianFormats(string input, string expected)
        {
            Assert.Equal(expected, BillFormat.NormalizeDate(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("not a date")]
        [InlineData("32/01/2024")]
        [InlineData("2024/13/01")]
        public void NormalizeDate_ReturnsEmptyForInvalidInput(string input)
        {
            Assert.Equal("", BillFormat.NormalizeDate(input));
        }

        [Fact]
        public void ToOracleProcDate_ConvertsIsoToDayMonthYear()
        {
            Assert.Equal("05/03/2024", BillFormat.ToOracleProcDate("2024-03-05"));
        }

        [Theory]
        [InlineData("1,234.50", "1234.5")]
        [InlineData("Rs. 500", "500")]
        [InlineData("₹12,00,000.75", "1200000.75")]   // Indian digit grouping
        [InlineData("INR 99.99", "99.99")]
        [InlineData("0", "0")]
        public void NormalizeAmount_StripsSymbolsAndSeparators(string input, string expected)
        {
            Assert.Equal(expected, BillFormat.NormalizeAmount(input));
        }

        [Theory]
        [InlineData("-5")]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData(null)]
        public void NormalizeAmount_RejectsNegativeOrNonNumeric(string input)
        {
            Assert.Equal("", BillFormat.NormalizeAmount(input));
        }
    }
}

using System;
using System.Globalization;

namespace bills_project.Services
{
    // Normalises bill dates and amounts so every page and both databases agree on one format.
    // Dates are stored as yyyy-MM-dd (what <input type="date"> sends); amounts as plain decimals.
    public static class BillFormat
    {
        // Day-first formats come before month-first ones because Indian bills are day-first.
        private static readonly string[] InputDateFormats =
        {
            "yyyy-MM-dd",
            "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy",
            "dd-MM-yy", "d-M-yy", "dd/MM/yy", "d/M/yy", "dd.MM.yy",
            "MM/dd/yyyy", "MM-dd-yyyy"
        };

        // Returns the date as yyyy-MM-dd, or an empty string if it can't be read.
        public static string NormalizeDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            DateTime d;
            if (DateTime.TryParseExact(raw.Trim(), InputDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return string.Empty;
        }

        // Converts a yyyy-MM-dd date to DD/MM/YYYY, the format the Oracle save_bill_secure procedure expects.
        public static string ToOracleProcDate(string isoDate)
        {
            DateTime d;
            if (DateTime.TryParseExact(isoDate ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            return isoDate;
        }

        // Strips currency symbols and thousands separators ("Rs. 1,234.50" -> "1234.50").
        // Returns an empty string if the result isn't a valid non-negative amount.
        public static string NormalizeAmount(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string cleaned = raw.Replace(",", "").Replace("₹", "").Replace("Rs.", "").Replace("Rs", "").Replace("INR", "").Trim();
            decimal amt;
            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out amt) && amt >= 0)
                return amt.ToString("0.##", CultureInfo.InvariantCulture);
            return string.Empty;
        }
    }
}

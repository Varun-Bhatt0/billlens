using System;
using System.Data;
using System.Globalization;

namespace bills_project.Services
{
    // Totals shown on the home page and the bill history page.
    public sealed class BillSummary
    {
        private static readonly CultureInfo IndianFormat = CultureInfo.GetCultureInfo("en-IN");

        public int Count { get; private set; }
        public decimal Total { get; private set; }
        public DateTime? LastSubmitted { get; private set; }

        public string TotalText => Total.ToString("N2", IndianFormat);        // 1,20,000.50
        public string LastSubmittedText => LastSubmitted?.ToString("d MMM yyyy", CultureInfo.InvariantCulture) ?? "";

        // Expects the columns returned by IDatabaseManager.GetBillsForUser (BillAmount, SubmittedOn).
        public static BillSummary From(DataTable bills)
        {
            var summary = new BillSummary();
            if (bills == null) return summary;
            foreach (DataRow row in bills.Rows)
            {
                summary.Count++;
                if (decimal.TryParse(Convert.ToString(row["BillAmount"], CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                    summary.Total += amount;
                if (TryDate(row["SubmittedOn"], out var submitted) && (summary.LastSubmitted == null || submitted > summary.LastSubmitted))
                    summary.LastSubmitted = submitted;
            }
            return summary;
        }

        private static bool TryDate(object value, out DateTime date)
        {
            if (value is DateTime dt) { date = dt; return true; }
            return DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }
    }
}

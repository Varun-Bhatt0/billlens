using System;
using System.Collections.Generic;

namespace bills_project.Services
{
    public class BillService
    {
        public const int MaxBillNumberLength = 50;

        private readonly IDatabaseManager _db;
        public BillService(IDatabaseManager db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // Validates and normalises the input, saves the bill and writes an audit entry.
        // Throws ArgumentException for invalid input; returns false if the database save failed.
        public bool SaveBill(string employeeId, int billType, string billNo, string billDate, string amount, string filePath = null)
        {
            if (string.IsNullOrWhiteSpace(employeeId))
                throw new ArgumentException("User Employee ID is required.");
            if (billType <= 0)
                throw new ArgumentException("A bill type is required.");

            string cleanNo = (billNo ?? "").Trim();
            string isoDate = BillFormat.NormalizeDate(billDate);
            string cleanAmount = BillFormat.NormalizeAmount(amount);
            if (cleanNo.Length == 0 || cleanNo.Length > MaxBillNumberLength || string.IsNullOrEmpty(isoDate) || string.IsNullOrEmpty(cleanAmount))
                throw new ArgumentException("A valid bill number, date, and amount are required.");
            if (DateTime.Parse(isoDate, System.Globalization.CultureInfo.InvariantCulture) > DateTime.Today.AddDays(1))
                throw new ArgumentException("The bill date can't be in the future.");

            var details = new Dictionary<string, string>
            {
                {"bill_no", cleanNo},
                {"bill_date", isoDate},
                {"bill_amt", cleanAmount}
            };
            bool saved = _db.SaveBill(employeeId, billType, details, filePath);
            if (saved)
                _db.LogAudit(employeeId, "SaveBill", $"Saved bill {cleanNo} ({isoDate}, {cleanAmount}){(filePath != null ? " with file" : "")}");
            return saved;
        }
    }
}

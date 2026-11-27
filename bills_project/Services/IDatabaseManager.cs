using System;
using System.Collections.Generic;
using System.Data;

namespace bills_project.Services
{
    public interface IDatabaseManager : IDisposable
    {
        // "Oracle" or "SQLite": which database this instance ended up using.
        string Provider { get; }

        Dictionary<string, object> GetUserLoginInfo(string employeeId);
        void UpdateLastLogin(string employeeId);
        bool SaveBill(string employeeId, int billType, Dictionary<string, string> details, string imagePath = null);
        List<KeyValuePair<int, string>> GetBillTypes();
        DataTable GetBillsForUser(string employeeId);
        void LogAudit(string employeeId, string action, string details);
        DataTable GetAuditLog(int maxRows = 500);
    }
}

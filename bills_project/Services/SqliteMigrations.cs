using System;
using System.Data.SQLite;

namespace bills_project.Services
{
    // Versioned schema for the SQLite database. The current version is stored in SQLite's built-in
    // PRAGMA user_version; each migration runs once, in order, inside a transaction.
    // To change the schema, add a new entry at the end. Never edit one that has already shipped.
    // The Oracle equivalent is db/oracle/schema.sql.
    public static class SqliteMigrations
    {
        private static readonly string[][] Migrations =
        {
            // 1: base schema. IF NOT EXISTS keeps databases created by older versions of the app working.
            new[]
            {
                @"CREATE TABLE IF NOT EXISTS users (
                    employee_id      TEXT PRIMARY KEY,
                    emp_name      TEXT NOT NULL,
                    password_hash TEXT NOT NULL,
                    is_admin      TEXT NOT NULL DEFAULT 'N' CHECK (is_admin IN ('Y','N')),
                    created_date  TEXT,
                    last_login    TEXT
                )",
                @"CREATE TABLE IF NOT EXISTS bill_type_master (
                    bill_type_no INTEGER PRIMARY KEY,
                    bill_type    TEXT NOT NULL UNIQUE,
                    description  TEXT,
                    is_active    TEXT NOT NULL DEFAULT 'Y' CHECK (is_active IN ('Y','N')),
                    created_date TEXT
                )",
                @"CREATE TABLE IF NOT EXISTS bill_applications (
                    application_sno  INTEGER PRIMARY KEY AUTOINCREMENT,
                    -- no FK to users: when Oracle fails mid-session, an Oracle user's bill still has to save here
                    employee_id         TEXT NOT NULL,
                    bill_type_no     INTEGER NOT NULL REFERENCES bill_type_master(bill_type_no),
                    application_date TEXT NOT NULL,
                    status           TEXT NOT NULL DEFAULT 'SUBMITTED'
                )",
                @"CREATE TABLE IF NOT EXISTS bill_details (
                    detail_sno      INTEGER PRIMARY KEY AUTOINCREMENT,
                    application_sno INTEGER NOT NULL REFERENCES bill_applications(application_sno) ON DELETE CASCADE,
                    bill_no         TEXT NOT NULL,
                    bill_date       TEXT NOT NULL,
                    bill_amount     REAL NOT NULL CHECK (bill_amount >= 0),
                    image_path      TEXT,
                    created_date    TEXT
                )",
                @"CREATE TABLE IF NOT EXISTS audit_log (
                    log_id   INTEGER PRIMARY KEY AUTOINCREMENT,
                    employee_id TEXT,
                    action   TEXT NOT NULL,
                    details  TEXT,
                    log_time TEXT NOT NULL
                )",
                @"INSERT OR IGNORE INTO bill_type_master (bill_type_no, bill_type, description, is_active, created_date) VALUES
                    (1, 'Medical Bills',   'Medical reimbursement',     'Y', datetime('now')),
                    (2, 'Travel Expenses', 'Official travel',           'Y', datetime('now')),
                    (3, 'Office Supplies', 'Stationery and supplies',   'Y', datetime('now')),
                    (4, 'Utility Bills',   'Electricity, water, phone', 'Y', datetime('now'))"
            },

            // 2: indexes for the queries the app actually runs.
            new[]
            {
                "CREATE INDEX IF NOT EXISTS ix_bill_applications_user_date ON bill_applications (employee_id, application_date DESC)",
                "CREATE INDEX IF NOT EXISTS ix_bill_details_application ON bill_details (application_sno)",
                "CREATE INDEX IF NOT EXISTS ix_audit_log_user ON audit_log (employee_id)"
            }
        };

        public static int LatestVersion => Migrations.Length;

        public static int CurrentVersion(SQLiteConnection conn)
        {
            using (var cmd = new SQLiteCommand("PRAGMA user_version", conn))
                return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static void Apply(SQLiteConnection conn)
        {
            int version = CurrentVersion(conn);
            for (int i = version; i < Migrations.Length; i++)
            {
                using (var tx = conn.BeginTransaction())
                {
                    foreach (var sql in Migrations[i])
                    {
                        using (var cmd = new SQLiteCommand(sql, conn, tx))
                            cmd.ExecuteNonQuery();
                    }
                    // PRAGMA can't take parameters; the value is our own loop counter, not user input.
                    using (var cmd = new SQLiteCommand($"PRAGMA user_version = {i + 1}", conn, tx))
                        cmd.ExecuteNonQuery();
                    tx.Commit();
                }
                SecureLogger.LogInfo("db.migrated", $"SQLite schema upgraded to version {i + 1}");
            }
        }
    }
}

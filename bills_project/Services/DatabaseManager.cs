using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Text.RegularExpressions;
using bills_project.App_Config;
using Oracle.ManagedDataAccess.Client;

namespace bills_project.Services
{
    public class DatabaseInitializationException : Exception
    {
        public DatabaseInitializationException(string message, Exception inner) : base(message, inner) { }
    }

    // One connection per request: Oracle when it's configured and reachable, otherwise SQLite.
    // All SQL is written once with @name parameters; for Oracle they are rewritten to :name.
    public sealed class DatabaseManager : IDatabaseManager
    {
        private IDbConnection _conn;
        private readonly bool _isOracle;

        // After Oracle fails, skip it for a while instead of waiting for the connect timeout on every request.
        private static readonly object OracleStateLock = new object();
        private static DateTime _oracleRetryAfterUtc = DateTime.MinValue;
        private static readonly TimeSpan OracleRetryDelay = TimeSpan.FromMinutes(5);

        // SQLite schema is checked once per database file per process.
        private static readonly ConcurrentDictionary<string, bool> MigratedDatabases = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public string Provider => _isOracle ? "Oracle" : "SQLite";

        public DatabaseManager()
        {
            if (TryOpenOracle(out var oracle))
            {
                _conn = oracle;
                _isOracle = true;
                return;
            }

            if (AppSettings.OracleConfigured && !AppSettings.UseSqliteFallback)
                throw new DatabaseInitializationException("Oracle is unavailable and the SQLite fallback is disabled.", null);

            try
            {
                _conn = OpenSqlite();
                _isOracle = false;
            }
            catch (Exception ex)
            {
                SecureLogger.LogCritical("db.init_failed", "Could not open Oracle or SQLite", ex);
                throw new DatabaseInitializationException("Failed to initialize both Oracle and SQLite databases.", ex);
            }
        }

        // ------------------------------------------------------------------ connections

        private static bool TryOpenOracle(out OracleConnection connection)
        {
            connection = null;
            if (!AppSettings.OracleConfigured) return false;
            lock (OracleStateLock)
            {
                if (DateTime.UtcNow < _oracleRetryAfterUtc) return false;
            }

            var builder = new OracleConnectionStringBuilder
            {
                UserID = AppSettings.OracleUser,
                Password = AppSettings.OraclePassword,
                DataSource = AppSettings.OracleDSN,
                Pooling = true,
                MinPoolSize = 1,
                MaxPoolSize = 20,
                ConnectionTimeout = 5 // seconds; fail over to SQLite quickly instead of the default 15
            };
            try
            {
                connection = new OracleConnection(builder.ConnectionString);
                connection.Open();
                return true;
            }
            catch (Exception ex)
            {
                connection?.Dispose();
                connection = null;
                lock (OracleStateLock) { _oracleRetryAfterUtc = DateTime.UtcNow + OracleRetryDelay; }
                SecureLogger.LogError("db.oracle_unavailable", $"Oracle connection failed; using SQLite for {OracleRetryDelay.TotalMinutes} minutes", ex);
                return false;
            }
        }

        private static SQLiteConnection OpenSqlite()
        {
            string fullPath = AppSettings.MapPath(AppSettings.SqliteDatabase);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = fullPath,
                Version = 3,
                ForeignKeys = true,                  // enforce the REFERENCES constraints
                JournalMode = SQLiteJournalModeEnum.Wal, // readers don't block the writer
                BusyTimeout = 5000,                  // wait up to 5 s for a lock instead of failing at once
                DateTimeFormat = SQLiteDateFormats.ISO8601
            };
            var conn = new SQLiteConnection(builder.ConnectionString);
            conn.Open();
            if (MigratedDatabases.TryAdd(fullPath, true))
            {
                try
                {
                    SqliteMigrations.Apply(conn);
                    if (AppSettings.SeedDemoUsers) SeedDemoUsers(conn);
                }
                catch
                {
                    MigratedDatabases.TryRemove(fullPath, out _);
                    throw;
                }
            }
            return conn;
        }

        // Creates the two demo logins described in the README, only in an empty users table.
        private static void SeedDemoUsers(SQLiteConnection conn)
        {
            using (var count = new SQLiteCommand("SELECT COUNT(*) FROM users", conn))
            {
                if (Convert.ToInt64(count.ExecuteScalar()) > 0) return;
            }
            var demo = new[]
            {
                new { Id = "test123", Name = "Test User", Password = "password123", Admin = "N" },
                new { Id = "admin123", Name = "Test Admin", Password = "admin123", Admin = "Y" }
            };
            foreach (var u in demo)
            {
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO users (employee_id, emp_name, password_hash, is_admin, created_date) VALUES (@employeeId, @name, @hash, @admin, @now)", conn))
                {
                    cmd.Parameters.AddWithValue("@employeeId", u.Id);
                    cmd.Parameters.AddWithValue("@name", u.Name);
                    cmd.Parameters.AddWithValue("@hash", BcryptService.GenerateHash(u.Password));
                    cmd.Parameters.AddWithValue("@admin", u.Admin);
                    cmd.Parameters.AddWithValue("@now", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
            }
            SecureLogger.Security("db.demo_users_seeded", "Created demo users test123 and admin123 (SEED_DEMO_USERS=true)");
        }

        // ------------------------------------------------------------------ command helpers

        private static readonly Regex NamedParam = new Regex(@"@(\w+)", RegexOptions.Compiled);

        private IDbCommand Command(string sql, params (string Name, object Value)[] parameters)
        {
            var cmd = _conn.CreateCommand();
            if (_isOracle)
            {
                ((OracleCommand)cmd).BindByName = true;
                sql = NamedParam.Replace(sql, ":$1");
            }
            cmd.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = _isOracle ? name : "@" + name;
                p.Value = value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            return cmd;
        }

        private DataTable Query(string sql, params (string, object)[] parameters)
        {
            var dt = new DataTable();
            using (var cmd = Command(sql, parameters))
            using (var reader = cmd.ExecuteReader())
            {
                dt.Load(reader);
            }
            return dt;
        }

        private int Execute(string sql, params (string, object)[] parameters)
        {
            using (var cmd = Command(sql, parameters))
            {
                return cmd.ExecuteNonQuery();
            }
        }

        // ------------------------------------------------------------------ users

        public Dictionary<string, object> GetUserLoginInfo(string employeeId)
        {
            try
            {
                var dt = Query("SELECT password_hash, is_admin, emp_name FROM users WHERE employee_id = @employeeId", ("employeeId", employeeId));
                if (dt.Rows.Count == 0) return null;
                var row = dt.Rows[0];
                return new Dictionary<string, object>
                {
                    { "password_hash", Convert.ToString(row["password_hash"]) },
                    { "is_admin", Convert.ToString(row["is_admin"]) },
                    { "emp_name", Convert.ToString(row["emp_name"]) }
                };
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("db.query_failed", "GetUserLoginInfo failed", ex, new { provider = Provider });
                return null;
            }
        }

        public void UpdateLastLogin(string employeeId)
        {
            try
            {
                Execute("UPDATE users SET last_login = @now WHERE employee_id = @employeeId", ("now", DateTime.Now), ("employeeId", employeeId));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("db.query_failed", "UpdateLastLogin failed", ex, new { provider = Provider });
            }
        }

        // ------------------------------------------------------------------ bills

        public bool SaveBill(string employeeId, int billType, Dictionary<string, string> details, string imagePath = null)
        {
            details.TryGetValue("bill_no", out string billNo);
            details.TryGetValue("bill_date", out string billDate);   // yyyy-MM-dd
            details.TryGetValue("bill_amt", out string billAmt);     // plain decimal
            try
            {
                return _isOracle
                    ? SaveBillOracle(employeeId, billType, billNo, billDate, billAmt, imagePath)
                    : SaveBillSqlite(employeeId, billType, billNo, billDate, billAmt, imagePath);
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("db.save_failed", "SaveBill failed", ex, new { provider = Provider, billType });
                return false;
            }
        }

        // Both rows are written in one transaction, so a failure can't leave an application without its details.
        private bool SaveBillSqlite(string employeeId, int billType, string billNo, string billDate, string billAmt, string imagePath)
        {
            var conn = (SQLiteConnection)_conn;
            using (var tx = conn.BeginTransaction())
            {
                long appSno;
                using (var cmd = new SQLiteCommand(
                    "INSERT INTO bill_applications (employee_id, bill_type_no, application_date, status) VALUES (@employeeId, @type, @now, 'SUBMITTED')", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@employeeId", employeeId);
                    cmd.Parameters.AddWithValue("@type", billType);
                    cmd.Parameters.AddWithValue("@now", DateTime.Now);
                    cmd.ExecuteNonQuery();
                    appSno = conn.LastInsertRowId;
                }
                using (var cmd = new SQLiteCommand(
                    @"INSERT INTO bill_details (application_sno, bill_no, bill_date, bill_amount, image_path, created_date)
                      VALUES (@app, @no, @date, @amt, @img, @now)", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@app", appSno);
                    cmd.Parameters.AddWithValue("@no", billNo);
                    cmd.Parameters.AddWithValue("@date", billDate);
                    cmd.Parameters.AddWithValue("@amt", decimal.Parse(billAmt, System.Globalization.CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("@img", (object)imagePath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@now", DateTime.Now);
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            return true;
        }

        // Calls save_bill_secure (db/oracle/schema.sql). Parameters are bound by position, so this works with
        // the original procedure and the one in the schema script. The procedure does both inserts and commits.
        private bool SaveBillOracle(string employeeId, int billType, string billNo, string billDate, string billAmt, string imagePath)
        {
            using (var cmd = new OracleCommand("save_bill_secure", (OracleConnection)_conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = false;
                cmd.Parameters.Add("p_employee_id", OracleDbType.Varchar2).Value = employeeId;
                cmd.Parameters.Add("p_type", OracleDbType.Int32).Value = billType;
                cmd.Parameters.Add("p_bill_no", OracleDbType.Varchar2).Value = billNo;
                cmd.Parameters.Add("p_bill_date", OracleDbType.Varchar2).Value = BillFormat.ToOracleProcDate(billDate); // DD/MM/YYYY
                cmd.Parameters.Add("p_bill_amt", OracleDbType.Varchar2).Value = billAmt;
                cmd.Parameters.Add("p_image_path", OracleDbType.Varchar2).Value = imagePath ?? "";
                cmd.Parameters.Add("p_ip", OracleDbType.Varchar2).Value = "";
                cmd.Parameters.Add("p_agent", OracleDbType.Varchar2).Value = "";
                cmd.Parameters.Add("out_app_sno", OracleDbType.Int32).Direction = ParameterDirection.Output;
                cmd.Parameters.Add("out_detail_sno", OracleDbType.Int32).Direction = ParameterDirection.Output;
                cmd.ExecuteNonQuery();
            }
            return true;
        }

        public List<KeyValuePair<int, string>> GetBillTypes()
        {
            var types = new List<KeyValuePair<int, string>>();
            try
            {
                var dt = Query("SELECT bill_type_no, bill_type FROM bill_type_master WHERE is_active = 'Y' ORDER BY bill_type_no");
                foreach (DataRow row in dt.Rows)
                    types.Add(new KeyValuePair<int, string>(Convert.ToInt32(row["bill_type_no"]), Convert.ToString(row["bill_type"])));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("db.query_failed", "GetBillTypes failed", ex, new { provider = Provider });
            }
            return types;
        }

        public DataTable GetBillsForUser(string employeeId)
        {
            try
            {
                return Query(@"SELECT a.application_sno AS ApplicationNo, b.bill_type AS BillType, d.bill_no AS BillNumber,
                                      d.bill_date AS BillDate, d.bill_amount AS BillAmount, a.status AS Status, a.application_date AS SubmittedOn
                               FROM bill_applications a
                               JOIN bill_type_master b ON a.bill_type_no = b.bill_type_no
                               JOIN bill_details d ON a.application_sno = d.application_sno
                               WHERE a.employee_id = @employeeId
                               ORDER BY a.application_date DESC", ("employeeId", employeeId));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("db.query_failed", "GetBillsForUser failed", ex, new { provider = Provider });
                return new DataTable();
            }
        }

        // ------------------------------------------------------------------ audit log

        public void LogAudit(string employeeId, string action, string details)
        {
            try
            {
                Execute("INSERT INTO audit_log (employee_id, action, details, log_time) VALUES (@employeeId, @action, @details, @now)",
                    ("employeeId", employeeId), ("action", action), ("details", details), ("now", DateTime.Now));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("db.audit_failed", "LogAudit failed", ex, new { provider = Provider, action });
            }
        }

        public DataTable GetAuditLog(int maxRows = 500)
        {
            try
            {
                string sql = _isOracle
                    ? "SELECT log_time, employee_id, action, details FROM audit_log ORDER BY log_id DESC FETCH FIRST @n ROWS ONLY"
                    : "SELECT log_time, employee_id, action, details FROM audit_log ORDER BY log_id DESC LIMIT @n";
                return Query(sql, ("n", maxRows));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("db.query_failed", "GetAuditLog failed", ex, new { provider = Provider });
                return new DataTable();
            }
        }

        public void Dispose()
        {
            try { _conn?.Dispose(); }
            catch (Exception ex) { SecureLogger.LogWarning("db.dispose_failed", "Error closing database connection", new { error = ex.Message }); }
            _conn = null;
        }
    }
}

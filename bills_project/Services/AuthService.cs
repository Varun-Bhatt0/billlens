using System;
using System.Collections.Generic;

namespace bills_project.Services
{
    public enum AuthOutcome { Success, InvalidCredentials, LockedOut }

    public sealed class AuthResult
    {
        public AuthOutcome Outcome { get; set; }
        public Dictionary<string, object> User { get; set; }
        public TimeSpan RetryAfter { get; set; }
        public bool Succeeded => Outcome == AuthOutcome.Success;
    }

    // Checks a employee ID/password pair and locks an account after too many failed attempts.
    // No logging or HttpContext here so it can be unit-tested; LoginPage logs the outcome.
    public class AuthService
    {
        private readonly IDatabaseManager _db;
        private readonly RateLimiter _failures;
        private readonly RateLimitPolicy _lockout;
        private static readonly Lazy<string> DummyHash = new Lazy<string>(() => BcryptService.GenerateHash(Guid.NewGuid().ToString()));

        public AuthService(IDatabaseManager db, RateLimiter failureTracker, RateLimitPolicy lockoutPolicy)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _failures = failureTracker ?? throw new ArgumentNullException(nameof(failureTracker));
            _lockout = lockoutPolicy ?? throw new ArgumentNullException(nameof(lockoutPolicy));
        }

        public AuthResult Authenticate(string employeeId, string password)
        {
            employeeId = (employeeId ?? "").Trim();
            string key = "login-fail:" + employeeId.ToUpperInvariant();

            // Locked accounts are rejected before the password is checked, so guessing stops working entirely.
            if (_failures.IsLimited(key, _lockout, out var retryAfter))
                return new AuthResult { Outcome = AuthOutcome.LockedOut, RetryAfter = retryAfter };

            var user = (employeeId.Length == 0 || string.IsNullOrEmpty(password)) ? null : _db.GetUserLoginInfo(employeeId);
            if (user != null && PasswordMatches(user, password))
            {
                _failures.Reset(key);
                return new AuthResult { Outcome = AuthOutcome.Success, User = user };
            }
            if (user == null && !string.IsNullOrEmpty(password))
            {
                // Spend the same bcrypt time for unknown employee IDs, so response time doesn't reveal which accounts exist.
                try { BcryptService.VerifyHash(password, DummyHash.Value); } catch (Exception) { }
            }

            // Unknown users count too, so the response doesn't reveal which employee IDs exist.
            _failures.Record(key);
            return new AuthResult { Outcome = AuthOutcome.InvalidCredentials };
        }

        private static bool PasswordMatches(Dictionary<string, object> user, string password)
        {
            string hash = user.TryGetValue("password_hash", out var h) ? h as string : null;
            if (string.IsNullOrEmpty(hash)) return false;
            try
            {
                return BcryptService.VerifyHash(password, hash);
            }
            catch (Exception)
            {
                // A malformed hash in the users table makes BCrypt throw; treat it as a failed login.
                return false;
            }
        }
    }
}

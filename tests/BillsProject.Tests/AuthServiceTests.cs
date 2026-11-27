using System;
using System.Collections.Generic;
using System.Data;
using bills_project.Services;
using Xunit;

namespace BillsProject.Tests
{
    public class AuthServiceTests
    {
        // Low cost factor keeps the tests fast; the app uses BcryptService.WorkFactor (12).
        private static readonly string GoodHash = BCrypt.Net.BCrypt.HashPassword("correct-horse", 4);
        private static readonly RateLimitPolicy Lockout = new RateLimitPolicy(3, TimeSpan.FromMinutes(15));

        private DateTime _now = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        private AuthService NewService(FakeDb db, out RateLimiter failures)
        {
            failures = new RateLimiter(() => _now);
            return new AuthService(db, failures, Lockout);
        }

        [Fact]
        public void CorrectPassword_Succeeds()
        {
            var auth = NewService(new FakeDb { ["100234"] = GoodHash }, out _);
            var result = auth.Authenticate("100234", "correct-horse");
            Assert.True(result.Succeeded);
            Assert.Equal("Test", result.User["emp_name"]);
        }

        [Fact]
        public void WrongPassword_Fails()
        {
            var auth = NewService(new FakeDb { ["100234"] = GoodHash }, out _);
            Assert.Equal(AuthOutcome.InvalidCredentials, auth.Authenticate("100234", "wrong").Outcome);
        }

        [Fact]
        public void UnknownUser_FailsTheSameWay()
        {
            var auth = NewService(new FakeDb(), out _);
            Assert.Equal(AuthOutcome.InvalidCredentials, auth.Authenticate("999999", "anything").Outcome);
        }

        [Theory]
        [InlineData("", "pw")]
        [InlineData("100234", "")]
        [InlineData(null, null)]
        public void BlankInput_Fails(string employeeId, string password)
        {
            var auth = NewService(new FakeDb { ["100234"] = GoodHash }, out _);
            Assert.False(auth.Authenticate(employeeId, password).Succeeded);
        }

        [Fact]
        public void MalformedHashInDatabase_FailsInsteadOfThrowing()
        {
            var auth = NewService(new FakeDb { ["100234"] = "not-a-bcrypt-hash" }, out _);
            Assert.Equal(AuthOutcome.InvalidCredentials, auth.Authenticate("100234", "correct-horse").Outcome);
        }

        [Fact]
        public void AccountLocksAfterRepeatedFailures_EvenForTheRightPassword()
        {
            var auth = NewService(new FakeDb { ["100234"] = GoodHash }, out _);
            for (int i = 0; i < 3; i++)
                auth.Authenticate("100234", "wrong");

            var result = auth.Authenticate("100234", "correct-horse");
            Assert.Equal(AuthOutcome.LockedOut, result.Outcome);
            Assert.True(result.RetryAfter > TimeSpan.Zero);
        }

        [Fact]
        public void LockExpiresAfterTheWindow()
        {
            var auth = NewService(new FakeDb { ["100234"] = GoodHash }, out _);
            for (int i = 0; i < 3; i++)
                auth.Authenticate("100234", "wrong");

            _now = _now.AddMinutes(16);
            Assert.True(auth.Authenticate("100234", "correct-horse").Succeeded);
        }

        [Fact]
        public void SuccessfulLoginClearsEarlierFailures()
        {
            var auth = NewService(new FakeDb { ["100234"] = GoodHash }, out _);
            auth.Authenticate("100234", "wrong");
            auth.Authenticate("100234", "wrong");
            Assert.True(auth.Authenticate("100234", "correct-horse").Succeeded);

            // the counter was reset, so two more failures don't lock the account
            auth.Authenticate("100234", "wrong");
            auth.Authenticate("100234", "wrong");
            Assert.True(auth.Authenticate("100234", "correct-horse").Succeeded);
        }

        [Fact]
        public void LockoutIsPerAccountAndIgnoresCase()
        {
            var db = new FakeDb { ["ABC1"] = GoodHash, ["xyz2"] = GoodHash };
            var auth = NewService(db, out _);
            auth.Authenticate("abc1", "wrong");
            auth.Authenticate("ABC1", "wrong");
            auth.Authenticate("Abc1", "wrong");

            Assert.Equal(AuthOutcome.LockedOut, auth.Authenticate("ABC1", "correct-horse").Outcome);
            Assert.True(auth.Authenticate("xyz2", "correct-horse").Succeeded);
        }

        private sealed class FakeDb : Dictionary<string, string>, IDatabaseManager
        {
            public FakeDb() : base(StringComparer.Ordinal) { }
            public string Provider => "Fake";

            public Dictionary<string, object> GetUserLoginInfo(string employeeId)
            {
                return TryGetValue(employeeId, out var hash)
                    ? new Dictionary<string, object> { { "password_hash", hash }, { "is_admin", "N" }, { "emp_name", "Test" } }
                    : null;
            }

            public void UpdateLastLogin(string employeeId) { }
            public bool SaveBill(string employeeId, int billType, Dictionary<string, string> details, string imagePath = null) => true;
            public List<KeyValuePair<int, string>> GetBillTypes() => new List<KeyValuePair<int, string>>();
            public DataTable GetBillsForUser(string employeeId) => new DataTable();
            public void LogAudit(string employeeId, string action, string details) { }
            public DataTable GetAuditLog(int maxRows = 500) => new DataTable();
            public void Dispose() { }
        }
    }
}

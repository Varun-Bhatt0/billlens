using System;
using System.Collections.Generic;
using System.Text.Json;
using bills_project.Services;
using Xunit;

namespace BillsProject.Tests
{
    public class LogFormatterTests
    {
        private static readonly DateTime Ts = new DateTime(2026, 11, 27, 10, 30, 0, DateTimeKind.Utc);

        private static JsonElement Parse(string line) => JsonDocument.Parse(line).RootElement;

        [Fact]
        public void ProducesOneValidJsonObjectWithStandardFields()
        {
            string line = LogFormatter.Format(Ts, "INFO", "auth.login_succeeded", "User signed in",
                new Dictionary<string, object> { { "requestId", "abc123" }, { "ip", "10.0.0.5" } },
                new { employeeId = "100234" });

            var json = Parse(line);
            Assert.Equal("2026-11-27T10:30:00.000Z", json.GetProperty("ts").GetString());
            Assert.Equal("INFO", json.GetProperty("level").GetString());
            Assert.Equal("auth.login_succeeded", json.GetProperty("event").GetString());
            Assert.Equal("abc123", json.GetProperty("requestId").GetString());
            Assert.Equal("100234", json.GetProperty("employeeId").GetString());
        }

        [Fact]
        public void NewlinesAreEscaped_SoInputCantForgeLogLines()
        {
            string line = LogFormatter.Format(Ts, "WARNING", "auth.login_failed", "bad login",
                properties: new { employeeId = "x\n{\"level\":\"INFO\",\"event\":\"fake\"}" });

            Assert.DoesNotContain("\n", line);
            Assert.Equal("x\n{\"level\":\"INFO\",\"event\":\"fake\"}", Parse(line).GetProperty("employeeId").GetString());
        }

        [Theory]
        [InlineData("password")]
        [InlineData("newPassword")]
        [InlineData("csrfToken")]
        [InlineData("password_hash")]
        [InlineData("Cookie")]
        public void SensitiveKeysAreRedacted(string key)
        {
            string line = LogFormatter.Format(Ts, "INFO", "e", "m",
                properties: new Dictionary<string, object> { { key, "s3cret" } });
            Assert.Equal("[REDACTED]", Parse(line).GetProperty(key).GetString());
        }

        [Fact]
        public void PropertiesCantOverwriteBaseFields()
        {
            string line = LogFormatter.Format(Ts, "ERROR", "real.event", "m", properties: new { level = "INFO", @event = "fake" });
            var json = Parse(line);
            Assert.Equal("ERROR", json.GetProperty("level").GetString());
            Assert.Equal("real.event", json.GetProperty("event").GetString());
        }

        [Fact]
        public void NumbersAndBooleansKeepTheirJsonTypes()
        {
            var json = Parse(LogFormatter.Format(Ts, "INFO", "e", "m", properties: new { count = 3, ok = true, amount = 12.5m }));
            Assert.Equal(JsonValueKind.Number, json.GetProperty("count").ValueKind);
            Assert.Equal(JsonValueKind.True, json.GetProperty("ok").ValueKind);
            Assert.Equal(12.5m, json.GetProperty("amount").GetDecimal());
        }

        [Fact]
        public void IncludesExceptionDetails()
        {
            var json = Parse(LogFormatter.Format(Ts, "ERROR", "e", "m", exception: new InvalidOperationException("boom")));
            Assert.Equal("System.InvalidOperationException", json.GetProperty("error.type").GetString());
            Assert.Equal("boom", json.GetProperty("error.message").GetString());
        }
    }
}

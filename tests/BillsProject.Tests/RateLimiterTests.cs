using System;
using bills_project.Services;
using Xunit;

namespace BillsProject.Tests
{
    public class RateLimiterTests
    {
        private DateTime _now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        private RateLimiter NewLimiter() => new RateLimiter(() => _now);

        [Fact]
        public void AllowsUpToTheLimitThenBlocks()
        {
            var limiter = NewLimiter();
            var policy = new RateLimitPolicy(3, TimeSpan.FromMinutes(1));

            for (int i = 0; i < 3; i++)
                Assert.True(limiter.TryAcquire("ip:1", policy, out _));

            Assert.False(limiter.TryAcquire("ip:1", policy, out var retryAfter));
            Assert.Equal(TimeSpan.FromMinutes(1), retryAfter);
        }

        [Fact]
        public void WindowSlides_OldHitsExpire()
        {
            var limiter = NewLimiter();
            var policy = new RateLimitPolicy(2, TimeSpan.FromMinutes(1));
            limiter.TryAcquire("k", policy, out _);
            _now = _now.AddSeconds(30);
            limiter.TryAcquire("k", policy, out _);
            Assert.False(limiter.TryAcquire("k", policy, out var retry));
            Assert.Equal(TimeSpan.FromSeconds(30), retry); // first hit leaves the window in 30 s

            _now = _now.AddSeconds(31);
            Assert.True(limiter.TryAcquire("k", policy, out _));
        }

        [Fact]
        public void KeysAreIndependent()
        {
            var limiter = NewLimiter();
            var policy = new RateLimitPolicy(1, TimeSpan.FromMinutes(1));
            Assert.True(limiter.TryAcquire("user:a", policy, out _));
            Assert.True(limiter.TryAcquire("user:b", policy, out _));
            Assert.False(limiter.TryAcquire("user:a", policy, out _));
        }

        [Fact]
        public void IsLimitedDoesNotRecordHits_RecordAndResetDo()
        {
            var limiter = NewLimiter();
            var policy = new RateLimitPolicy(2, TimeSpan.FromMinutes(15));
            Assert.False(limiter.IsLimited("fail:x", policy, out _));
            Assert.False(limiter.IsLimited("fail:x", policy, out _));

            limiter.Record("fail:x");
            limiter.Record("fail:x");
            Assert.True(limiter.IsLimited("fail:x", policy, out _));

            limiter.Reset("fail:x");
            Assert.False(limiter.IsLimited("fail:x", policy, out _));
        }

        [Theory]
        [InlineData("5 per minute", 5, 60)]
        [InlineData("100 per hour", 100, 3600)]
        [InlineData("10/second", 10, 1)]
        [InlineData("5 per 15 minutes", 5, 900)]
        [InlineData("  20 PER MINUTE ", 20, 60)]
        public void PolicyParsesConfigText(string text, int limit, int seconds)
        {
            Assert.True(RateLimitPolicy.TryParse(text, out var policy));
            Assert.Equal(limit, policy.Limit);
            Assert.Equal(TimeSpan.FromSeconds(seconds), policy.Window);
        }

        [Theory]
        [InlineData("")]
        [InlineData("fast")]
        [InlineData("0 per minute")]
        [InlineData("5 per fortnight")]
        public void PolicyRejectsBadText(string text)
        {
            Assert.False(RateLimitPolicy.TryParse(text, out _));
        }
    }
}

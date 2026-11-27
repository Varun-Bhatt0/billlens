using System;
using System.Globalization;
using System.Web;
using bills_project.App_Config;
using bills_project.Services;

namespace bills_project.Infrastructure
{
    // The app's rate limits (Web.config RATE_LIMITS) and the shared in-memory limiter.
    public static class RateLimits
    {
        public static readonly RateLimiter Limiter = new RateLimiter();

        // Separate tracker for failed logins (AuthService), so lockouts and request limits don't interfere.
        public static readonly RateLimiter LoginFailures = new RateLimiter();

        public static RateLimitPolicy General => AppSettings.RateLimit("general", new RateLimitPolicy(120, TimeSpan.FromMinutes(1)));
        public static RateLimitPolicy Login => AppSettings.RateLimit("login", new RateLimitPolicy(10, TimeSpan.FromMinutes(1)));
        public static RateLimitPolicy Upload => AppSettings.RateLimit("upload", new RateLimitPolicy(10, TimeSpan.FromMinutes(1)));
        public static RateLimitPolicy Save => AppSettings.RateLimit("save", new RateLimitPolicy(20, TimeSpan.FromMinutes(1)));

        // Returns true if allowed. When blocked, logs a security event and sets retryAfter.
        public static bool Allow(string bucket, string subject, RateLimitPolicy policy, out TimeSpan retryAfter)
        {
            if (Limiter.TryAcquire(bucket + ":" + subject, policy, out retryAfter))
                return true;
            SecureLogger.Security("ratelimit.blocked", $"Rate limit '{bucket}' exceeded",
                new { bucket, subject, limit = policy.Limit, windowSeconds = (int)policy.Window.TotalSeconds, retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds) });
            return false;
        }

        // Ends the request with 429 Too Many Requests and a Retry-After header.
        public static void RespondTooManyRequests(HttpContext context, TimeSpan retryAfter)
        {
            var response = context.Response;
            response.Clear();
            response.StatusCode = 429;
            response.StatusDescription = "Too Many Requests";
            response.TrySkipIisCustomErrors = true;
            response.AppendHeader("Retry-After", Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture));
            response.ContentType = "text/plain";
            response.Write("Too many requests. Please wait a moment and try again.");
            context.ApplicationInstance.CompleteRequest();
        }

        public static string Describe(TimeSpan retryAfter)
        {
            int seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            return seconds < 90 ? $"{seconds} seconds" : $"{(int)Math.Ceiling(seconds / 60.0)} minutes";
        }
    }
}

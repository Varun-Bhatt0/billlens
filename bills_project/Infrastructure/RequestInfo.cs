using System;
using System.Collections.Generic;
using System.Web;

namespace bills_project.Infrastructure
{
    // Per-request details used by logging and rate limiting.
    public static class RequestInfo
    {
        private const string RequestIdKey = "bills.RequestId";
        public const string RequestIdHeader = "X-Request-ID";

        // A short random ID for the current request, also sent back in the X-Request-ID header
        // and shown on the error page, so a user's report can be matched to log lines.
        public static string RequestId(HttpContext context)
        {
            if (context == null) return null;
            if (context.Items[RequestIdKey] is string id) return id;
            id = Guid.NewGuid().ToString("N").Substring(0, 12);
            context.Items[RequestIdKey] = id;
            return id;
        }

        // The direct client address. X-Forwarded-For is deliberately ignored: it is set by the client and
        // trusting it would let anyone dodge rate limits. Behind a trusted reverse proxy, change this.
        public static string ClientIp(HttpContext context)
        {
            return context?.Request?.UserHostAddress ?? "unknown";
        }

        // Fields added to every log line written during a request.
        public static IDictionary<string, object> LogContext()
        {
            var ctx = HttpContext.Current;
            if (ctx == null) return null;
            var fields = new Dictionary<string, object>
            {
                { "requestId", RequestId(ctx) },
                { "ip", ClientIp(ctx) }
            };
            try
            {
                fields["path"] = ctx.Request.Path;
                fields["method"] = ctx.Request.HttpMethod;
            }
            catch (HttpException) { /* request not available yet */ }
            if (ctx.User?.Identity?.IsAuthenticated == true)
                fields["user"] = ctx.User.Identity.Name;
            return fields;
        }
    }
}

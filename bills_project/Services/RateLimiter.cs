using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace bills_project.Services
{
    // "N per <unit>" limit, e.g. "5 per minute", "100 per hour", "5 per 15 minutes".
    public sealed class RateLimitPolicy
    {
        public int Limit { get; }
        public TimeSpan Window { get; }

        public RateLimitPolicy(int limit, TimeSpan window)
        {
            if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
            if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
            Limit = limit;
            Window = window;
        }

        private static readonly Regex Pattern = new Regex(
            @"^\s*(\d+)\s*(?:per|/)\s*(\d+)?\s*(second|minute|hour|day)s?\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryParse(string text, out RateLimitPolicy policy)
        {
            policy = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var m = Pattern.Match(text);
            if (!m.Success) return false;

            int limit = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            int count = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
            if (limit <= 0 || count <= 0) return false;

            TimeSpan unit;
            switch (m.Groups[3].Value.ToLowerInvariant())
            {
                case "second": unit = TimeSpan.FromSeconds(1); break;
                case "minute": unit = TimeSpan.FromMinutes(1); break;
                case "hour": unit = TimeSpan.FromHours(1); break;
                default: unit = TimeSpan.FromDays(1); break;
            }
            policy = new RateLimitPolicy(limit, TimeSpan.FromTicks(unit.Ticks * count));
            return true;
        }

        public override string ToString() => $"{Limit} per {Window}";
    }

    // In-memory sliding-window rate limiter. Thread-safe.
    // Counts are per worker process: behind a load balancer or in a web garden each process keeps its own counts.
    public sealed class RateLimiter
    {
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _hits = new ConcurrentDictionary<string, Queue<DateTime>>();
        private readonly Func<DateTime> _clock;
        private int _callsSinceCleanup;
        private const int CleanupEvery = 1000;

        public RateLimiter() : this(() => DateTime.UtcNow) { }

        // The clock is injectable so tests can move time forward.
        public RateLimiter(Func<DateTime> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        // Records a hit and returns true if the key is still under the limit; otherwise returns false and how long to wait.
        public bool TryAcquire(string key, RateLimitPolicy policy, out TimeSpan retryAfter)
        {
            return Check(key, policy, record: true, out retryAfter);
        }

        // Returns true if the key has already used up its limit (does not record a hit).
        public bool IsLimited(string key, RateLimitPolicy policy, out TimeSpan retryAfter)
        {
            return !Check(key, policy, record: false, out retryAfter);
        }

        // Records a hit without checking (e.g. a failed login attempt).
        public void Record(string key)
        {
            var queue = _hits.GetOrAdd(key, _ => new Queue<DateTime>());
            lock (queue) { queue.Enqueue(_clock()); }
            MaybeCleanup();
        }

        // Forgets all hits for a key (e.g. after a successful login).
        public void Reset(string key)
        {
            _hits.TryRemove(key, out _);
        }

        private bool Check(string key, RateLimitPolicy policy, bool record, out TimeSpan retryAfter)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            DateTime now = _clock();
            var queue = _hits.GetOrAdd(key, _ => new Queue<DateTime>());
            bool allowed;
            lock (queue)
            {
                Trim(queue, now - policy.Window);
                if (queue.Count < policy.Limit)
                {
                    if (record) queue.Enqueue(now);
                    retryAfter = TimeSpan.Zero;
                    allowed = true;
                }
                else
                {
                    retryAfter = queue.Peek() + policy.Window - now;
                    if (retryAfter < TimeSpan.Zero) retryAfter = TimeSpan.Zero;
                    allowed = false;
                }
            }
            MaybeCleanup();
            return allowed;
        }

        private static void Trim(Queue<DateTime> queue, DateTime cutoff)
        {
            while (queue.Count > 0 && queue.Peek() <= cutoff)
                queue.Dequeue();
        }

        // Drops keys with no recent hits so memory doesn't grow with every IP/user ever seen.
        private void MaybeCleanup()
        {
            if (System.Threading.Interlocked.Increment(ref _callsSinceCleanup) < CleanupEvery) return;
            System.Threading.Interlocked.Exchange(ref _callsSinceCleanup, 0);
            DateTime cutoff = _clock() - TimeSpan.FromDays(1);
            foreach (var entry in _hits)
            {
                lock (entry.Value)
                {
                    Trim(entry.Value, cutoff);
                    if (entry.Value.Count == 0) _hits.TryRemove(entry.Key, out _);
                }
            }
        }

        public int TrackedKeyCount => _hits.Count;
    }
}

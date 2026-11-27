using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace bills_project.Services
{
    // Turns a log event into one line of JSON ("JSON Lines"), so logs can be searched and filtered by field
    // (e.g. all events for one requestId or user) instead of grepping free text.
    // Pure and dependency-free so it can be unit-tested.
    public static class LogFormatter
    {
        private static readonly string[] SensitiveKeyParts = { "password", "pwd", "secret", "token", "cookie", "hash" };

        public static string Format(
            DateTime timestampUtc,
            string level,
            string eventName,
            string message,
            IDictionary<string, object> context = null,
            object properties = null,
            Exception exception = null)
        {
            var fields = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("ts", timestampUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)),
                new KeyValuePair<string, object>("level", level),
                new KeyValuePair<string, object>("event", eventName),
                new KeyValuePair<string, object>("msg", message)
            };

            if (context != null)
                foreach (var kv in context)
                    if (kv.Value != null) fields.Add(kv);

            foreach (var kv in ToDictionary(properties))
                fields.Add(new KeyValuePair<string, object>(kv.Key, IsSensitive(kv.Key) ? "[REDACTED]" : kv.Value));

            if (exception != null)
            {
                fields.Add(new KeyValuePair<string, object>("error.type", exception.GetType().FullName));
                fields.Add(new KeyValuePair<string, object>("error.message", exception.Message));
                fields.Add(new KeyValuePair<string, object>("error.stack", exception.ToString()));
            }

            var sb = new StringBuilder(256);
            sb.Append('{');
            bool first = true;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in fields)
            {
                if (!seen.Add(kv.Key)) continue; // first value for a key wins (base fields can't be overwritten)
                if (!first) sb.Append(',');
                first = false;
                AppendString(sb, kv.Key);
                sb.Append(':');
                AppendValue(sb, kv.Value);
            }
            sb.Append('}');
            return sb.ToString();
        }

        public static bool IsSensitive(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            string k = key.ToLowerInvariant();
            return SensitiveKeyParts.Any(part => k.Contains(part));
        }

        // Accepts an anonymous object (new { user = "x" }) or a dictionary.
        public static IEnumerable<KeyValuePair<string, object>> ToDictionary(object properties)
        {
            if (properties == null) yield break;
            if (properties is IDictionary<string, object> dict)
            {
                foreach (var kv in dict) yield return kv;
                yield break;
            }
            if (properties is IDictionary legacy)
            {
                foreach (DictionaryEntry e in legacy) yield return new KeyValuePair<string, object>(Convert.ToString(e.Key, CultureInfo.InvariantCulture), e.Value);
                yield break;
            }
            foreach (var p in properties.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length == 0)
                    yield return new KeyValuePair<string, object>(p.Name, p.GetValue(properties));
            }
        }

        private static void AppendValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int _:
                case long _:
                case short _:
                case byte _:
                case uint _:
                case ulong _:
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("R", CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("R", CultureInfo.InvariantCulture)); break;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); break;
                case DateTime dt: AppendString(sb, dt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture)); break;
                case TimeSpan ts: sb.Append(((long)ts.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)); break;
                default: AppendString(sb, Convert.ToString(value, CultureInfo.InvariantCulture)); break;
            }
        }

        // JSON string escaping. Newlines become \n, so a value can never start a fake log line (log injection).
        public static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20 || c == (char)0x2028 || c == (char)0x2029)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}

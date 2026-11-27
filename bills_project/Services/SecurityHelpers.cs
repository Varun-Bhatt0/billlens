using System;
using System.Text;

namespace bills_project.Services
{
    public static class SecurityHelpers
    {
        // Constant-time comparison to prevent timing attacks
        public static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null)
                return false;
            if (a.Length == 0 && b.Length == 0)
                return true;
            if (a.Length != b.Length)
                return false;
            int result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }

        // Reads the is_admin value however it was stored ("Y", "1", "true", bool or int).
        public static bool IsAdmin(object value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            if (value is int i) return i != 0;
            string s = value.ToString().Trim();
            return s.Equals("Y", StringComparison.OrdinalIgnoreCase)
                || s.Equals("true", StringComparison.OrdinalIgnoreCase)
                || s == "1";
        }
    }
}

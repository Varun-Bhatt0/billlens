namespace bills_project.Services
{
    public static class BcryptService
    {
        // Cost 12 = 2^12 rounds (~250 ms per hash on a typical server): slow enough to resist guessing, fast enough for logins.
        public const int WorkFactor = 12;

        public static string GenerateHash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
        }

        public static bool VerifyHash(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
    }
}

using System;
using System.IO;

namespace bills_project.Services
{
    // Checks that an uploaded file's first bytes match its extension, so a renamed .exe or script
    // can't be stored as "bill.pdf". The extension check alone only looks at the name.
    public static class FileSignature
    {
        private static readonly byte[] Pdf = { 0x25, 0x50, 0x44, 0x46, 0x2D };             // %PDF-
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF };

        public const int HeaderLength = 8;

        public static bool Matches(string extension, byte[] header)
        {
            if (header == null || string.IsNullOrEmpty(extension)) return false;
            switch (extension.ToLowerInvariant())
            {
                case ".pdf": return StartsWith(header, Pdf);
                case ".png": return StartsWith(header, Png);
                case ".jpg":
                case ".jpeg": return StartsWith(header, Jpeg);
                default: return false;
            }
        }

        // Reads the header from a stream and puts the stream back where it was.
        public static bool Matches(string extension, Stream stream)
        {
            if (stream == null || !stream.CanRead) return false;
            long start = stream.CanSeek ? stream.Position : 0;
            var header = new byte[HeaderLength];
            int read = 0, n;
            while (read < header.Length && (n = stream.Read(header, read, header.Length - read)) > 0)
                read += n;
            if (stream.CanSeek) stream.Position = start;
            if (read < header.Length) Array.Resize(ref header, read);
            return Matches(extension, header);
        }

        private static bool StartsWith(byte[] data, byte[] prefix)
        {
            if (data.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (data[i] != prefix[i]) return false;
            return true;
        }
    }
}

using System;
using System.IO;
using System.Text;

namespace bills_project.Services.Ocr
{
    // An 8-bit grayscale image (0 = black, 255 = white) held as a plain byte array.
    // The OCR preprocessing works on this type so it has no dependency on System.Drawing and can be
    // unit-tested and benchmarked on any OS. Files are exchanged with Tesseract and Poppler as PGM.
    public sealed class GrayImage
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }

        public GrayImage(int width, int height, byte[] pixels = null)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Image must not be empty.");
            Width = width;
            Height = height;
            Pixels = pixels ?? new byte[checked(width * height)];
            if (Pixels.Length != width * height) throw new ArgumentException("Pixel buffer size doesn't match the dimensions.");
        }

        public byte this[int x, int y]
        {
            get => Pixels[y * Width + x];
            set => Pixels[y * Width + x] = value;
        }

        public static GrayImage Filled(int width, int height, byte value)
        {
            var img = new GrayImage(width, height);
            for (int i = 0; i < img.Pixels.Length; i++) img.Pixels[i] = value;
            return img;
        }

        public GrayImage Clone() => new GrayImage(Width, Height, (byte[])Pixels.Clone());

        // ---- PGM (binary "P5") ----

        public static GrayImage ReadPgm(string path)
        {
            using (var stream = File.OpenRead(path))
                return ReadPgm(stream);
        }

        public static GrayImage ReadPgm(Stream stream)
        {
            string magic = ReadToken(stream);
            if (magic != "P5") throw new InvalidDataException("Only binary PGM (P5) files are supported.");
            int width = int.Parse(ReadToken(stream));
            int height = int.Parse(ReadToken(stream));
            int maxVal = int.Parse(ReadToken(stream));
            if (maxVal <= 0 || maxVal > 255) throw new InvalidDataException("Only 8-bit PGM files are supported.");

            var pixels = new byte[checked(width * height)];
            int read = 0, n;
            while (read < pixels.Length && (n = stream.Read(pixels, read, pixels.Length - read)) > 0) read += n;
            if (read != pixels.Length) throw new InvalidDataException("PGM file is truncated.");
            if (maxVal != 255)
                for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(pixels[i] * 255 / maxVal);
            return new GrayImage(width, height, pixels);
        }

        public void WritePgm(string path)
        {
            using (var stream = File.Create(path))
            {
                byte[] header = Encoding.ASCII.GetBytes($"P5\n{Width} {Height}\n255\n");
                stream.Write(header, 0, header.Length);
                stream.Write(Pixels, 0, Pixels.Length);
            }
        }

        // Reads one whitespace-separated header token, skipping # comments.
        private static string ReadToken(Stream s)
        {
            var sb = new StringBuilder();
            int c;
            while ((c = s.ReadByte()) != -1)
            {
                if (c == '#') { while ((c = s.ReadByte()) != -1 && c != '\n') { } continue; }
                if (char.IsWhiteSpace((char)c)) { if (sb.Length > 0) break; continue; }
                sb.Append((char)c);
            }
            if (sb.Length == 0) throw new InvalidDataException("Unexpected end of PGM header.");
            return sb.ToString();
        }
    }
}

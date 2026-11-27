using System;
using System.Collections.Generic;
using System.Linq;

namespace bills_project.Services.Ocr
{
    public sealed class PreprocessResult
    {
        public GrayImage Image { get; set; }
        public double SkewDegrees { get; set; }        // rotation that was corrected
        public double ScaleFactor { get; set; }        // resize applied so text reaches TargetTextHeight
        public double TextHeight { get; set; }         // estimated height of a text line before scaling, in pixels
        public double Noise { get; set; }              // estimated noise level of the input (grey levels), for diagnostics
    }

    // Cleans up a photo or scan of a bill before Tesseract reads it:
    //  1. flatten uneven lighting (shadows, gradients) by dividing by an estimate of the paper background
    //  2. detect and undo small rotations (deskew)
    //  3. resize so text lines are about the height Tesseract's LSTM model reads best
    //  4. stretch contrast so ink is near black and paper near white
    // Tesseract does its own binarisation afterwards, which works far better on the cleaned image.
    public static class ImagePreprocessor
    {
        public const double TargetTextHeight = 34;   // pixels per text line band (cap height ~24 px)
        private const double MinScale = 0.6, MaxScale = 4.0;
        private const double MaxSkew = 6.0;

        public static PreprocessResult Process(GrayImage input)
        {
            // Measured on the untouched image (before flattening clips the paper to white); reported for diagnostics.
            double noise = EstimateNoise(input);
            var img = FlattenBackground(input);

            // No denoising filter here: on the benchmark a median filter erased thin strokes of small print and
            // lowered accuracy at every noise level, so speckle is left to Tesseract's own binarisation.

            // Deskew before measuring text height: tilted lines would merge into one tall band.
            double skew = EstimateSkew(img);
            if (Math.Abs(skew) >= 0.15)
                img = Rotate(img, -skew);

            double textHeight = EstimateTextHeight(img);
            // If no text lines could be measured (very noisy or tiny images), assume a small image needs enlarging.
            double scale = textHeight > 0
                ? Clamp(TargetTextHeight / textHeight, MinScale, MaxScale)
                : Clamp(1600.0 / img.Width, 1.0, MaxScale);
            if (Math.Abs(scale - 1.0) > 0.08)
                img = Resize(img, scale);

            img = StretchContrast(img, 0.01, 0.99);

            return new PreprocessResult { Image = img, SkewDegrees = skew, ScaleFactor = scale, TextHeight = textHeight, Noise = noise };
        }

        // ------------------------------------------------------------------ lighting

        // Estimates the paper colour at every point (text removed by a max filter on a downsampled copy,
        // then smoothed) and divides it out, so a shadowed corner ends up as white as the rest of the page.
        public static GrayImage FlattenBackground(GrayImage src)
        {
            const int factor = 8;
            int sw = Math.Max(1, src.Width / factor), sh = Math.Max(1, src.Height / factor);
            var small = new GrayImage(sw, sh);
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    int sum = 0, count = 0;
                    for (int dy = 0; dy < factor; dy++)
                    {
                        int yy = y * factor + dy; if (yy >= src.Height) break;
                        for (int dx = 0; dx < factor; dx++)
                        {
                            int xx = x * factor + dx; if (xx >= src.Width) break;
                            sum += src[xx, yy]; count++;
                        }
                    }
                    small[x, y] = (byte)(sum / Math.Max(1, count));
                }

            var background = BoxBlur(MaxFilter(small, 3), 3);

            var result = new GrayImage(src.Width, src.Height);
            for (int y = 0; y < src.Height; y++)
            {
                double fy = Math.Min(sh - 1.0, Math.Max(0, (y + 0.5) / factor - 0.5));
                for (int x = 0; x < src.Width; x++)
                {
                    double fx = Math.Min(sw - 1.0, Math.Max(0, (x + 0.5) / factor - 0.5));
                    double bg = Bilinear(background, fx, fy);
                    double v = bg < 1 ? 255 : src[x, y] * 255.0 / bg;
                    result[x, y] = ToByte(v);
                }
            }
            return result;
        }

        public static GrayImage StretchContrast(GrayImage src, double lowPercentile, double highPercentile)
        {
            var hist = new int[256];
            foreach (byte p in src.Pixels) hist[p]++;
            int lo = Percentile(hist, src.Pixels.Length, lowPercentile);
            int hi = Percentile(hist, src.Pixels.Length, highPercentile);
            if (hi - lo < 16) return src.Clone(); // flat image (blank page); nothing to stretch
            var result = new GrayImage(src.Width, src.Height);
            double k = 255.0 / (hi - lo);
            for (int i = 0; i < src.Pixels.Length; i++)
                result.Pixels[i] = ToByte((src.Pixels[i] - lo) * k);
            return result;
        }

        // ------------------------------------------------------------------ deskew

        // Projection-profile method: rotate the dark pixels by candidate angles and keep the angle whose
        // row histogram is "spikiest" (text lines line up with rows). Coarse search, then a fine one.
        // Returns the page's rotation in degrees, counter-clockwise positive; Rotate(img, -skew) undoes it.
        public static double EstimateSkew(GrayImage src)
        {
            // work on a copy at most ~1200 px wide for speed
            double s = Math.Min(1.0, 1200.0 / src.Width);
            var img = s < 1.0 ? Resize(src, s) : src;
            byte threshold = OtsuThreshold(img);
            var dark = new List<(int X, int Y)>();
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    if (img[x, y] < threshold) dark.Add((x, y));
            if (dark.Count < 200 || dark.Count > img.Pixels.Length / 2) return 0;

            double best = 0, bestScore = double.MinValue;
            foreach (double a in Range(-MaxSkew, MaxSkew, 0.25))
            {
                double sc = ProfileScore(dark, img.Width, img.Height, a);
                if (sc > bestScore) { bestScore = sc; best = a; }
            }
            double coarse = best;
            foreach (double a in Range(coarse - 0.25, coarse + 0.25, 0.05))
            {
                double sc = ProfileScore(dark, img.Width, img.Height, a);
                if (sc > bestScore) { bestScore = sc; best = a; }
            }
            // best is the rotation that levels the text; the page itself is turned the opposite way
            return Math.Round(-best, 2);
        }

        private static double ProfileScore(List<(int X, int Y)> dark, int width, int height, double degrees)
        {
            double r = degrees * Math.PI / 180, sin = Math.Sin(r), cos = Math.Cos(r);
            int pad = (int)(width * Math.Abs(sin)) + 2;
            var rows = new int[height + 2 * pad];
            foreach (var (x, y) in dark)
            {
                int ry = (int)Math.Round(y * cos - x * sin) + pad;
                if (ry >= 0 && ry < rows.Length) rows[ry]++;
            }
            double sum = 0;
            for (int i = 1; i < rows.Length; i++) { double d = rows[i] - rows[i - 1]; sum += d * d; }
            return sum;
        }

        // Rotates by `degrees` (counter-clockwise positive) about the centre, filling new corners with white.
        public static GrayImage Rotate(GrayImage src, double degrees)
        {
            double r = degrees * Math.PI / 180, sin = Math.Sin(r), cos = Math.Cos(r);
            var result = new GrayImage(src.Width, src.Height);
            double cx = src.Width / 2.0, cy = src.Height / 2.0;
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    double dx = x - cx, dy = y - cy;
                    double sx = cos * dx - sin * dy + cx;
                    double sy = sin * dx + cos * dy + cy;
                    result[x, y] = sx < 0 || sy < 0 || sx > src.Width - 1 || sy > src.Height - 1
                        ? (byte)255
                        : ToByte(Bilinear(src, sx, sy));
                }
            return result;
        }

        // ------------------------------------------------------------------ text size and noise

        // Median height of the text line bands. Each row is first blurred sideways so the letters of a line
        // merge into one solid band and leftover speckle averages out; then rows with ink are grouped into bands.
        public static double EstimateTextHeight(GrayImage img)
        {
            int radius = Math.Max(3, img.Width / 150);
            var smeared = new GrayImage(img.Width, img.Height);
            for (int y = 0; y < img.Height; y++)
            {
                long sum = 0; int count = 0;
                for (int x = -radius; x < img.Width; x++)
                {
                    int add = x + radius, drop = x - radius - 1;
                    if (add < img.Width) { sum += img[add, y]; count++; }
                    if (drop >= 0) { sum -= img[drop, y]; count--; }
                    if (x >= 0) smeared[x, y] = (byte)(sum / count);
                }
            }
            byte threshold = OtsuThreshold(smeared);
            var rowInk = new double[img.Height];
            for (int y = 0; y < img.Height; y++)
            {
                int c = 0;
                for (int x = 0; x < img.Width; x++) if (smeared[x, y] < threshold) c++;
                rowInk[y] = c;
            }
            // smooth vertically so thin gaps inside a line (sparse small print) don't split it into pieces
            var smooth = new double[img.Height];
            for (int y = 0; y < img.Height; y++)
            {
                double sum = 0; int n = 0;
                for (int d = -2; d <= 2; d++) { int yy = y + d; if (yy >= 0 && yy < img.Height) { sum += rowInk[yy]; n++; } }
                smooth[y] = sum / n;
            }

            double minInk = Math.Max(2, img.Width / 200.0);
            var bands = new List<(int Height, double Ink)>();
            int start = -1; double ink = 0;
            for (int y = 0; y <= img.Height; y++)
            {
                bool inBand = y < img.Height && smooth[y] >= minInk;
                if (inBand) { if (start < 0) { start = y; ink = 0; } ink += rowInk[y]; }
                else if (start >= 0)
                {
                    int h = y - start;
                    if (h >= 5 && h < img.Height / 5) bands.Add((h, ink));
                    start = -1;
                }
            }
            if (bands.Count < 3) return 0;

            // ink-weighted median: full lines of text count for more than fragments and rules
            bands.Sort((a, b) => a.Height.CompareTo(b.Height));
            double total = bands.Sum(b => b.Ink), acc = 0;
            foreach (var b in bands)
            {
                acc += b.Ink;
                if (acc >= total / 2) return b.Height;
            }
            return bands[bands.Count / 2].Height;
        }

        // Median absolute difference between each pixel and its 3x3 median, on a sample of pixels.
        public static double EstimateNoise(GrayImage img)
        {
            var diffs = new List<int>();
            var buf = new byte[9];
            int step = Math.Max(1, (img.Width * img.Height) / 40000);
            for (int i = 0; i < img.Pixels.Length; i += step)
            {
                int x = i % img.Width, y = i / img.Width;
                if (x == 0 || y == 0 || x >= img.Width - 1 || y >= img.Height - 1) continue;
                diffs.Add(Math.Abs(img[x, y] - MedianAt(img, x, y, buf)));
            }
            if (diffs.Count == 0) return 0;
            diffs.Sort();
            return diffs[diffs.Count / 2] * 1.4826 * 1.5; // scale so it roughly matches the noise's standard deviation
        }

        private static byte MedianAt(GrayImage img, int x, int y, byte[] v)
        {
            int k = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    v[k++] = img[x + dx, y + dy];
            // partial insertion sort is enough for 9 values
            for (int i = 1; i < 9; i++) { byte t = v[i]; int j = i - 1; while (j >= 0 && v[j] > t) { v[j + 1] = v[j]; j--; } v[j + 1] = t; }
            return v[4];
        }

        // ------------------------------------------------------------------ primitives

        public static GrayImage Resize(GrayImage src, double scale)
        {
            int w = Math.Max(1, (int)Math.Round(src.Width * scale)), h = Math.Max(1, (int)Math.Round(src.Height * scale));
            var result = new GrayImage(w, h);
            if (scale < 1.0)
            {
                // area averaging when shrinking, so thin strokes don't disappear
                double inv = 1.0 / scale;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int x0 = (int)(x * inv), y0 = (int)(y * inv);
                        int x1 = Math.Min(src.Width, (int)((x + 1) * inv)), y1 = Math.Min(src.Height, (int)((y + 1) * inv));
                        int sum = 0, count = 0;
                        for (int yy = y0; yy < Math.Max(y0 + 1, y1); yy++)
                            for (int xx = x0; xx < Math.Max(x0 + 1, x1); xx++) { sum += src[Math.Min(xx, src.Width - 1), Math.Min(yy, src.Height - 1)]; count++; }
                        result[x, y] = (byte)(sum / count);
                    }
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    double sy = Math.Min(src.Height - 1.0, Math.Max(0, (y + 0.5) / scale - 0.5));
                    for (int x = 0; x < w; x++)
                    {
                        double sx = Math.Min(src.Width - 1.0, Math.Max(0, (x + 0.5) / scale - 0.5));
                        result[x, y] = ToByte(Bilinear(src, sx, sy));
                    }
                }
            }
            return result;
        }

        public static byte OtsuThreshold(GrayImage img)
        {
            var hist = new int[256];
            foreach (byte p in img.Pixels) hist[p]++;
            long total = img.Pixels.Length, sumAll = 0;
            for (int i = 0; i < 256; i++) sumAll += (long)i * hist[i];
            long wB = 0, sumB = 0;
            double best = -1; int threshold = 128;
            for (int t = 0; t < 256; t++)
            {
                wB += hist[t]; if (wB == 0) continue;
                long wF = total - wB; if (wF == 0) break;
                sumB += (long)t * hist[t];
                double mB = (double)sumB / wB, mF = (double)(sumAll - sumB) / wF;
                double between = (double)wB * wF * (mB - mF) * (mB - mF);
                if (between > best) { best = between; threshold = t; }
            }
            // Otsu's t is the last grey level of the dark class; callers test "pixel < threshold", so return t + 1.
            return (byte)Math.Min(255, threshold + 1);
        }

        private static GrayImage MaxFilter(GrayImage src, int radius)
        {
            var result = new GrayImage(src.Width, src.Height);
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    byte m = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int yy = y + dy; if (yy < 0 || yy >= src.Height) continue;
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int xx = x + dx; if (xx < 0 || xx >= src.Width) continue;
                            if (src[xx, yy] > m) m = src[xx, yy];
                        }
                    }
                    result[x, y] = m;
                }
            return result;
        }

        private static GrayImage BoxBlur(GrayImage src, int radius)
        {
            var result = new GrayImage(src.Width, src.Height);
            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    int sum = 0, count = 0;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int yy = y + dy; if (yy < 0 || yy >= src.Height) continue;
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int xx = x + dx; if (xx < 0 || xx >= src.Width) continue;
                            sum += src[xx, yy]; count++;
                        }
                    }
                    result[x, y] = (byte)(sum / count);
                }
            return result;
        }

        private static double Bilinear(GrayImage img, double x, double y)
        {
            int x0 = (int)x, y0 = (int)y;
            int x1 = Math.Min(x0 + 1, img.Width - 1), y1 = Math.Min(y0 + 1, img.Height - 1);
            double fx = x - x0, fy = y - y0;
            double top = img[x0, y0] * (1 - fx) + img[x1, y0] * fx;
            double bottom = img[x0, y1] * (1 - fx) + img[x1, y1] * fx;
            return top * (1 - fy) + bottom * fy;
        }

        private static int Percentile(int[] hist, int total, double p)
        {
            long target = (long)(total * p), acc = 0;
            for (int i = 0; i < 256; i++) { acc += hist[i]; if (acc >= target) return i; }
            return 255;
        }

        private static IEnumerable<double> Range(double from, double to, double step)
        {
            for (double a = from; a <= to + 1e-9; a += step) yield return a;
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
        private static byte ToByte(double v) => v <= 0 ? (byte)0 : v >= 255 ? (byte)255 : (byte)(v + 0.5);
    }
}

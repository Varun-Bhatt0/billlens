using System;
using System.IO;
using bills_project.Services.Ocr;
using Xunit;

namespace BillsProject.Tests
{
    public class OcrPreprocessingTests
    {
        // A white page with dark horizontal bars standing in for lines of text.
        private static GrayImage FakePage(int width = 800, int height = 1000, int lineHeight = 14, int pitch = 40, byte background = 255)
        {
            var img = GrayImage.Filled(width, height, background);
            for (int top = 60; top + lineHeight < height - 60; top += pitch)
                for (int y = top; y < top + lineHeight; y++)
                    for (int x = 80; x < width - 80; x++)
                        if ((x / 9) % 4 != 3) img[x, y] = 20; // gaps between "words"
            return img;
        }

        [Theory]
        [InlineData(3.0)]
        [InlineData(-2.0)]
        [InlineData(0.0)]
        public void EstimateSkew_FindsTheRotation(double degrees)
        {
            var page = ImagePreprocessor.Rotate(FakePage(), degrees);
            Assert.InRange(ImagePreprocessor.EstimateSkew(page), degrees - 0.3, degrees + 0.3);
        }

        [Fact]
        public void Process_StraightensARotatedPage()
        {
            var result = ImagePreprocessor.Process(ImagePreprocessor.Rotate(FakePage(), 2.5));
            Assert.InRange(result.SkewDegrees, 2.2, 2.8);
            Assert.InRange(ImagePreprocessor.EstimateSkew(result.Image), -0.3, 0.3);
        }

        [Fact]
        public void EstimateTextHeight_MeasuresLineBands()
        {
            Assert.InRange(ImagePreprocessor.EstimateTextHeight(FakePage(lineHeight: 14)), 12, 18);
        }

        [Fact]
        public void Process_EnlargesSmallText()
        {
            var result = ImagePreprocessor.Process(FakePage(lineHeight: 10, pitch: 24));
            Assert.True(result.ScaleFactor > 2.0, $"scale was {result.ScaleFactor}");
        }

        [Fact]
        public void FlattenBackground_RemovesAShadowGradient()
        {
            var page = FakePage();
            for (int y = 0; y < page.Height; y++)
                for (int x = 0; x < page.Width; x++)
                    page[x, y] = (byte)Math.Max(0, page[x, y] - 90 * x / page.Width); // darker towards the right
            var flat = ImagePreprocessor.FlattenBackground(page);
            // paper on the dark side ends up nearly as white as on the light side
            Assert.True(flat[page.Width - 40, 30] > 230, $"right edge {flat[page.Width - 40, 30]}");
            Assert.True(flat[40, 30] > 230);
        }

        [Fact]
        public void OtsuThreshold_SeparatesInkFromPaper()
        {
            byte t = ImagePreprocessor.OtsuThreshold(FakePage());
            Assert.InRange(t, 21, 254);
        }

        [Fact]
        public void Pgm_RoundTrips()
        {
            var img = FakePage(64, 48);
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pgm");
            try
            {
                img.WritePgm(path);
                var back = GrayImage.ReadPgm(path);
                Assert.Equal(img.Width, back.Width);
                Assert.Equal(img.Pixels, back.Pixels);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Pgm_RejectsOtherFormats()
        {
            using (var ms = new MemoryStream(System.Text.Encoding.ASCII.GetBytes("P2\n2 2\n255\n0 0 0 0")))
                Assert.Throws<InvalidDataException>(() => GrayImage.ReadPgm(ms));
        }
    }
}

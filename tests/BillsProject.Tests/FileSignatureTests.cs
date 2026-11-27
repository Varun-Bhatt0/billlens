using System.IO;
using System.Text;
using bills_project.Services;
using Xunit;

namespace BillsProject.Tests
{
    public class FileSignatureTests
    {
        private static readonly byte[] PdfHeader = Encoding.ASCII.GetBytes("%PDF-1.7\n");
        private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
        private static readonly byte[] ExeHeader = { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 }; // "MZ"

        [Theory]
        [InlineData(".pdf")]
        [InlineData(".PDF")]
        public void AcceptsRealPdf(string ext) => Assert.True(FileSignature.Matches(ext, PdfHeader));

        [Fact]
        public void AcceptsRealPng() => Assert.True(FileSignature.Matches(".png", PngHeader));

        [Theory]
        [InlineData(".jpg")]
        [InlineData(".jpeg")]
        public void AcceptsRealJpeg(string ext) => Assert.True(FileSignature.Matches(ext, JpegHeader));

        [Theory]
        [InlineData(".pdf")]
        [InlineData(".png")]
        [InlineData(".jpg")]
        public void RejectsExecutableRenamedAsBill(string ext) => Assert.False(FileSignature.Matches(ext, ExeHeader));

        [Fact]
        public void RejectsMismatchedType() => Assert.False(FileSignature.Matches(".png", PdfHeader));

        [Fact]
        public void RejectsUnknownExtensionAndTinyFiles()
        {
            Assert.False(FileSignature.Matches(".gif", PdfHeader));
            Assert.False(FileSignature.Matches(".pdf", new byte[] { 0x25 }));
            Assert.False(FileSignature.Matches(".pdf", (byte[])null));
        }

        [Fact]
        public void StreamOverloadRestoresPosition()
        {
            using (var ms = new MemoryStream(PdfHeader))
            {
                Assert.True(FileSignature.Matches(".pdf", ms));
                Assert.Equal(0, ms.Position);
            }
        }
    }
}

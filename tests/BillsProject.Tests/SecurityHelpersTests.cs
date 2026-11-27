using bills_project.Services;
using Xunit;

namespace BillsProject.Tests
{
    public class SecurityHelpersTests
    {
        [Theory]
        [InlineData("abc", "abc", true)]
        [InlineData("abc", "abd", false)]
        [InlineData("abc", "abcd", false)]
        [InlineData("", "", true)]
        [InlineData(null, "abc", false)]
        [InlineData("abc", null, false)]
        public void FixedTimeEquals(string a, string b, bool expected)
        {
            Assert.Equal(expected, SecurityHelpers.FixedTimeEquals(a, b));
        }

        [Theory]
        [InlineData("Y", true)]
        [InlineData("y", true)]
        [InlineData("1", true)]
        [InlineData("true", true)]
        [InlineData("N", false)]
        [InlineData("0", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsAdmin_ReadsDatabaseFlag(string value, bool expected)
        {
            Assert.Equal(expected, SecurityHelpers.IsAdmin(value));
        }

        [Fact]
        public void IsAdmin_AcceptsBoolAndInt()
        {
            Assert.True(SecurityHelpers.IsAdmin(true));
            Assert.True(SecurityHelpers.IsAdmin(1));
            Assert.False(SecurityHelpers.IsAdmin(0));
        }
    }
}

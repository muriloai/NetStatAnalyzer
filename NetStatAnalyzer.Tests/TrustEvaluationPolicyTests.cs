using System.Collections.Generic;
using NetStatAnalyzer.Domain.Entities;
using NetStatAnalyzer.Domain.Policies;
using Xunit;

namespace NetStatAnalyzer.Tests
{
    public class TrustEvaluationPolicyTests
    {
        [Theory]
        [InlineData("192.168.1.1:8080", "192.168.1.1")]
        [InlineData("10.0.0.1:443", "10.0.0.1")]
        [InlineData("[2001:db8::1]:80", "2001:db8::1")]
        [InlineData("[::1]:5000", "::1")]
        [InlineData("0.0.0.0:0", "0.0.0.0:0")]
        [InlineData("*:*", "*:*")]
        [InlineData("[::]:0", "[::]:0")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void ExtractIP_ShouldExtractCleanIP_FromAddressWithPort(string? input, string expected)
        {
            var result = TrustEvaluationPolicy.ExtractIP(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("0.0.0.0:0", true)]
        [InlineData("*:*", true)]
        [InlineData("*.*", true)]
        [InlineData("0.0.0.0", true)]
        [InlineData("[::]:0", true)]
        [InlineData("[::]", true)]
        [InlineData("::", true)]
        [InlineData("", true)]
        [InlineData(null, true)]
        [InlineData("192.168.1.10:80", false)]
        [InlineData("8.8.8.8:53", false)]
        [InlineData("127.0.0.1:5000", false)]
        public void IsWildcardOrListeningAddress_ShouldIdentifyListeningAndWildcards(string? address, bool expected)
        {
            var result = TrustEvaluationPolicy.IsWildcardOrListeningAddress(address);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void IsTrusted_WhenProcessNameAndForeignIPMatch_ShouldReturnTrue()
        {
            var rules = new List<AllowlistRule>
            {
                new AllowlistRule
                {
                    ProcessName = "chrome.exe",
                    IP = "142.250.190.46",
                    ForeignAddress = "142.250.190.46:443"
                }
            };

            bool trusted = TrustEvaluationPolicy.IsTrusted(
                processName: "chrome.exe",
                foreignAddress: "142.250.190.46:443",
                localAddress: "192.168.1.100:54321",
                rules: rules);

            Assert.True(trusted);
        }

        [Fact]
        public void IsTrusted_ShouldBeCaseInsensitiveForProcessName()
        {
            var rules = new List<AllowlistRule>
            {
                new AllowlistRule
                {
                    ProcessName = "CHROME.EXE",
                    IP = "142.250.190.46",
                    ForeignAddress = "142.250.190.46:443"
                }
            };

            bool trusted = TrustEvaluationPolicy.IsTrusted(
                processName: "chrome.exe",
                foreignAddress: "142.250.190.46:443",
                localAddress: "192.168.1.100:54321",
                rules: rules);

            Assert.True(trusted);
        }

        [Fact]
        public void IsTrusted_WhenForeignIPDiffers_ShouldReturnFalse()
        {
            var rules = new List<AllowlistRule>
            {
                new AllowlistRule
                {
                    ProcessName = "chrome.exe",
                    IP = "142.250.190.46",
                    ForeignAddress = "142.250.190.46:443"
                }
            };

            bool trusted = TrustEvaluationPolicy.IsTrusted(
                processName: "chrome.exe",
                foreignAddress: "1.1.1.1:443",
                localAddress: "192.168.1.100:54321",
                rules: rules);

            Assert.False(trusted);
        }

        [Fact]
        public void IsTrusted_WhenListeningSocketMatchesLocalAddressRule_ShouldReturnTrue()
        {
            var rules = new List<AllowlistRule>
            {
                new AllowlistRule
                {
                    ProcessName = "node.exe",
                    IP = "0.0.0.0",
                    LocalAddress = "0.0.0.0:3000",
                    ForeignAddress = "0.0.0.0:0"
                }
            };

            bool trusted = TrustEvaluationPolicy.IsTrusted(
                processName: "node.exe",
                foreignAddress: "0.0.0.0:0",
                localAddress: "0.0.0.0:3000",
                rules: rules);

            Assert.True(trusted);
        }

        [Fact]
        public void IsTrusted_WhenProcessNameIsEmpty_ShouldReturnFalse()
        {
            var rules = new List<AllowlistRule>
            {
                new AllowlistRule
                {
                    ProcessName = "node.exe",
                    IP = "0.0.0.0",
                    LocalAddress = "0.0.0.0:3000"
                }
            };

            bool trusted = TrustEvaluationPolicy.IsTrusted(
                processName: "",
                foreignAddress: "0.0.0.0:0",
                localAddress: "0.0.0.0:3000",
                rules: rules);

            Assert.False(trusted);
        }
    }
}

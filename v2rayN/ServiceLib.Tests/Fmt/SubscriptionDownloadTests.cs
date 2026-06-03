using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using ServiceLib.Common;
using ServiceLib.Enums;
using ServiceLib.Handler.Fmt;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class SubscriptionDownloadTests
{
    private const string SampleVlessUri1 =
        "vless://00000000-0000-4000-8000-000000000001@node1.example.com:443?encryption=none&security=reality&sni=www.example.com&fp=chrome&pbk=ZGVtb1B1YmxpY0tleTE&sid=abc123&type=tcp#Node%201";

    private const string SampleVlessUri2 =
        "vless://00000000-0000-4000-8000-000000000002@node2.example.com:443?encryption=none&security=reality&sni=www.example.org&fp=chrome&pbk=ZGVtb1B1YmxpY0tleTI&sid=def456&type=tcp#Node%202";

    private static string SampleBase64Subscription =>
        Utils.Base64Encode($"{SampleVlessUri1}\n{SampleVlessUri2}");

    [Fact]
    public void ResolveConfig_Base64Subscription_ShouldParseAllLines()
    {
        var strData = Utils.Base64Decode(SampleBase64Subscription);
        var lines = strData.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        lines.Length.Should().Be(2);

        foreach (var line in lines)
        {
            var profile = FmtHandler.ResolveConfig(line, out var msg);
            profile.Should().NotBeNull(msg);
            profile!.ConfigType.Should().Be(EConfigType.VLESS);
        }
    }

    [Fact]
    public void IsBase64String_Base64Subscription_WithTrailingNewline_ShouldStillDecode()
    {
        var base64 = SampleBase64Subscription;

        Utils.IsBase64String(base64).Should().BeTrue();
        Utils.IsBase64String(base64 + "\n").Should().BeTrue("trailing newline should not break base64 detection");
        Utils.IsBase64String(base64 + "\r\n").Should().BeTrue("trailing CRLF should not break base64 detection");

        var decoded = Utils.Base64Decode(base64 + "\n");
        decoded.Should().StartWith("vless://");
    }

    [Fact]
    public void DecodeDownloadedContent_GzipWrappedBase64Subscription_ShouldDecodeAndParseVless()
    {
        var plainText = Encoding.UTF8.GetBytes(SampleBase64Subscription);

        byte[] gzipped;
        using (var output = new MemoryStream())
        {
            using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(plainText, 0, plainText.Length);
            }
            gzipped = output.ToArray();
        }

        gzipped[0].Should().Be(0x1F);
        gzipped[1].Should().Be(0x8B);

        var decoded = Utils.DecodeDownloadedContent(gzipped);
        decoded.Should().Be(SampleBase64Subscription);

        var subscription = Utils.Base64Decode(decoded);
        var firstLine = subscription.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];
        var profile = FmtHandler.ResolveConfig(firstLine, out var msg);
        profile.Should().NotBeNull(msg);
        profile!.ConfigType.Should().Be(EConfigType.VLESS);
        profile.Address.Should().Be("node1.example.com");
    }
}

using System.IO.Compression;

namespace EggSharp.Core.Tests;

public sealed class AuxbrainFramingTests {
    private static readonly byte[] Payload = [0xfb, 0xff, 0x00, 0x10, 0x3e];

    [Fact]
    public void FromBase64Loose_RepairsSpacesAndPadding() {
        var canonical = Convert.ToBase64String(Payload);
        var mangled = canonical.Replace('+', ' ').TrimEnd('=');
        Assert.Equal(Payload, AuxbrainFraming.FromBase64Loose(mangled));
    }

    [Fact]
    public void FormBody_EscapesBase64() {
        Assert.Equal("data=" + Uri.EscapeDataString(Convert.ToBase64String(Payload)), AuxbrainFraming.FormBody(Payload));
        Assert.DoesNotContain("+", AuxbrainFraming.FormBody(Payload)[5..]);
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("zlib")]
    [InlineData("deflate")]
    public void Decompress_InflatesEachFormat(string format) {
        var plain = Enumerable.Range(0, 4096).Select(i => (byte)(i % 7)).ToArray();
        Assert.Equal(plain, AuxbrainFraming.Decompress(Compress(plain, format)));
    }

    [Fact]
    public void Decompress_ReturnsUncompressedInputUnchanged() {
        byte[] plain = [0x0a, 0x03, 0x61, 0x62, 0x63];
        Assert.Equal(plain, AuxbrainFraming.Decompress(plain));
    }

    [Theory]
    [InlineData("gzip")]
    [InlineData("zlib")]
    public void Decompress_PastCap_Throws(string format) {
        var bomb = Compress(new byte[AuxbrainFraming.MaxInflatedBytes + 1], format);
        Assert.Throws<PayloadTooLargeException>(() => AuxbrainFraming.Decompress(bomb));
    }

    private static byte[] Compress(byte[] plain, string format) {
        using var output = new MemoryStream();
        using (Stream stream = format switch {
            "gzip" => new GZipStream(output, CompressionLevel.Fastest),
            "zlib" => new ZLibStream(output, CompressionLevel.Fastest),
            _ => new DeflateStream(output, CompressionLevel.Fastest)
        }) {
            stream.Write(plain);
        }
        return output.ToArray();
    }
}

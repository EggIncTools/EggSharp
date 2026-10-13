using System.IO.Compression;
using System.Text;

namespace EggSharp.Storage.Tests;

public sealed class StorageCompressionTests {
    private static readonly byte[] Compressible = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("{\"eid\":\"EI1234567890123456\",\"eb\":1.5e21}", 64)));

    [Fact]
    public void Compress_BelowThreshold_IsRawEnvelope() {
        byte[] small = [1, 2, 3];
        Assert.Equal([0xEB, 0x00, 1, 2, 3], StorageCompression.Compress(small, new StorageCompressionStrategy(StorageCompressionAlgorithm.GZip)));
    }

    [Fact]
    public void Compress_Incompressible_FallsBackToRaw() {
        var noise = new byte[1000];
        new Random(7).NextBytes(noise);
        var stored = StorageCompression.Compress(noise, new StorageCompressionStrategy(StorageCompressionAlgorithm.GZip));
        Assert.Equal(StorageCompressionAlgorithm.Raw, (StorageCompressionAlgorithm)stored[1]);
        Assert.Equal(noise, StorageCompression.Decompress(stored));
    }

    [Theory]
    [InlineData(StorageCompressionAlgorithm.GZip)]
    [InlineData(StorageCompressionAlgorithm.Brotli)]
    [InlineData(StorageCompressionAlgorithm.Zstd)]
    public void RoundTrip_EachAlgorithm(StorageCompressionAlgorithm algorithm) {
        var stored = StorageCompression.Compress(Compressible, new StorageCompressionStrategy(algorithm));
        Assert.Equal((byte)algorithm, stored[1]);
        Assert.Equal(Compressible, StorageCompression.Decompress(stored));
    }

    [Fact]
    public void DefaultStrategies_CarryNoDictionary() {
        var stored = StorageCompression.Compress(Compressible, StorageCompressionStrategy.AccountGraph);
        Assert.Equal([0xEB, 0x03, 0x00], stored[..3]);
        Assert.Equal(Compressible, StorageCompression.Decompress(stored));
        Assert.Equal(StorageDictionary.None, StorageCompressionStrategy.CoopStatus.DictionaryId);
    }

    [Fact]
    public void Zstd_WithRegisteredDictionary_RoundTripsUnderItsId() {
        var dictionary = StorageDictionary.Register(211, "test-211", Compressible[..200]);
        var stored = StorageCompression.Compress(Compressible, StorageCompressionStrategy.AccountGraph.WithDictionary(dictionary));
        Assert.Equal([0xEB, 0x03, 211], stored[..3]);
        Assert.Equal(Compressible, StorageCompression.Decompress(stored));
    }

    [Fact]
    public void Decompress_GZipEnvelopeBuiltByHand() {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal)) {
            gzip.Write(Compressible);
        }
        byte[] stored = [0xEB, 0x01, .. output.ToArray()];
        Assert.Equal(Compressible, StorageCompression.Decompress(stored));
    }

    [Fact]
    public void Decompress_NotEnveloped_Throws() {
        Assert.Throws<InvalidDataException>(() => StorageCompression.Decompress([0x1F, 0x8B, 0x08]));
    }

    [Fact]
    public void Decompress_UnknownAlgorithm_Throws() {
        Assert.Throws<InvalidDataException>(() => StorageCompression.Decompress([0xEB, 0x09, 0x00]));
    }

    [Fact]
    public void Decompress_UnregisteredDictionary_ThrowsNamingId() {
        var e = Assert.Throws<InvalidDataException>(() => StorageCompression.Decompress([0xEB, 0x03, 0xC9, 0x28, 0xB5, 0x2F, 0xFD]));
        Assert.Contains("201", e.Message);
    }

    [Fact]
    public void SetCoopStatus_SwitchesWriteDictionary() {
        var original = StorageCompressionStrategy.CoopStatus.Dictionary;
        var replacement = StorageDictionary.Register(210, "test-210", Compressible[..200]);
        try {
            StorageCompressionStrategy.SetCoopStatus(replacement);
            Assert.Equal(210, StorageCompression.Compress(Compressible, StorageCompressionStrategy.CoopStatus)[2]);
        } finally {
            StorageCompressionStrategy.SetCoopStatus(original);
        }
    }
}

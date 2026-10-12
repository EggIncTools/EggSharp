namespace EggSharp.Storage.Tests;

public sealed class StorageDictionaryTests {
    private static readonly byte[] ZstdDictionaryMagic = [0x37, 0xA4, 0x30, 0xEC];

    [Fact]
    public void EmbeddedDictionaries_LoadAsZstdDictionaries() {
        Assert.Equal(ZstdDictionaryMagic, StorageDictionary.Accounts1.Bytes[..4]);
        Assert.Equal(ZstdDictionaryMagic, StorageDictionary.CoopStatus1.Bytes[..4]);
        Assert.Equal(1, StorageDictionary.Accounts1.Id);
        Assert.Equal(2, StorageDictionary.CoopStatus1.Id);
    }

    [Fact]
    public void Register_IdZero_Throws() {
        Assert.Throws<ArgumentOutOfRangeException>(() => StorageDictionary.Register(StorageDictionary.None, "zero", [1]));
    }

    [Fact]
    public void Register_SameBytes_ReturnsExisting() {
        var first = StorageDictionary.Register(220, "test-220", [1, 2, 3]);
        Assert.Same(first, StorageDictionary.Register(220, "test-220-again", [1, 2, 3]));
    }

    [Fact]
    public void Register_DifferentBytes_Throws() {
        StorageDictionary.Register(221, "test-221", [1, 2, 3]);
        Assert.Throws<InvalidOperationException>(() => StorageDictionary.Register(221, "test-221", [9]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Register_ReservedIdWithOtherBytes_Throws(byte id) {
        Assert.Throws<InvalidOperationException>(() => StorageDictionary.Register(id, "impostor", [9, 9, 9]));
    }

    [Fact]
    public void TryGet_Unknown_IsFalse() {
        Assert.False(StorageDictionary.TryGet(230, out _));
    }
}

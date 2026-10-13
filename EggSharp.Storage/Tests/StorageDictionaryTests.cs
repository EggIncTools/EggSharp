namespace EggSharp.Storage.Tests;

public sealed class StorageDictionaryTests {
    [Fact]
    public void PinnedIds_NeverChange() {
        Assert.Equal(1, StorageDictionary.Accounts1Id);
        Assert.Equal(2, StorageDictionary.CoopStatus1Id);
    }

    [Fact]
    public void Assembly_EmbedsNoDictionary() {
        Assert.DoesNotContain(typeof(StorageDictionary).Assembly.GetManifestResourceNames(), n => n.EndsWith(".zdict", StringComparison.Ordinal));
    }

    [Fact]
    public void Registry_IdsAreUniqueAndNonZero() {
        var ids = StorageDictionary.Registry.Select(d => d.Id).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
        Assert.DoesNotContain(StorageDictionary.None, ids);
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

    [Fact]
    public void Get_Unknown_Throws() {
        Assert.Throws<InvalidDataException>(() => StorageDictionary.Get(200));
    }

    [Fact]
    public void TryGet_Unknown_IsFalse() {
        Assert.False(StorageDictionary.TryGet(230, out _));
    }
}

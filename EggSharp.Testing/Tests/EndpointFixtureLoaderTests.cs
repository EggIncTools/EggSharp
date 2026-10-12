namespace EggSharp.Testing.Tests;

public sealed class EndpointFixtureLoaderTests : IDisposable {
    private const string Eid = "EI1234567890123456";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "eggsharp-fixtures-" + Guid.NewGuid().ToString("N"));

    public EndpointFixtureLoaderTests() {
        Write("default/ei/get_periodicals.bin", 1);
        Write("default/ei/coop_status.bin", 2);
        Write($"eids/{Eid}/ei/coop_status.bin", 3);
        Write("eids/EI0000000000000001/ei/coop_status.bin", 4);
        Write("staged/ei/get_periodicals.bin", 5);
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void Load_ReadsDefaultTierKeyedByPathWithoutExtension() {
        var fixtures = new EndpointFixtureLoader(_root).Load();

        Assert.Equal(new byte[] { 1 }, fixtures["ei/get_periodicals"]);
        Assert.Equal(new byte[] { 2 }, fixtures["ei/coop_status"]);
        Assert.Equal(2, fixtures.Count);
    }

    [Fact]
    public void Load_EidTierOverridesDefault() {
        var fixtures = new EndpointFixtureLoader(_root).Load(Eid);

        Assert.Equal(new byte[] { 3 }, fixtures["ei/coop_status"]);
        Assert.Equal(new byte[] { 1 }, fixtures["ei/get_periodicals"]);
    }

    [Fact]
    public void Load_UnknownEidFallsBackToDefault() =>
        Assert.Equal(new byte[] { 2 }, new EndpointFixtureLoader(_root).Load("EI9999999999999999")["ei/coop_status"]);

    [Fact]
    public void LoadStaged_ReadsOnlyTheStagedTier() =>
        Assert.Equal(new byte[] { 5 }, Assert.Single(new EndpointFixtureLoader(_root).LoadStaged()).Value);

    [Fact]
    public void Eids_ListsEidDirectoriesInOrder() =>
        Assert.Equal(new[] { "EI0000000000000001", Eid }, new EndpointFixtureLoader(_root).Eids());

    [Theory]
    [InlineData("..")]
    [InlineData("../default")]
    [InlineData("")]
    public void Load_RejectsEidsThatEscapeTheTier(string eid) =>
        Assert.Throws<ArgumentException>(() => new EndpointFixtureLoader(_root).Load(eid));

    [Fact]
    public async Task Handler_ServesTheResolvedTier() {
        using var handler = new EndpointFixtureLoader(_root).Handler(Eid);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://auxbrain.test/") };

        using var response = await client.PostAsync("ei/coop_status", null);

        Assert.Equal(new byte[] { 3 }, await response.Content.ReadAsByteArrayAsync());
    }

    private void Write(string relative, byte value) {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [value]);
    }
}

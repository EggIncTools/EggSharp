namespace EggSharp.Testing;

public sealed class EndpointFixtureLoader(string root) {
    public const string DefaultTier = "default";
    public const string EidsTier = "eids";
    public const string StagedTier = "staged";

    public IReadOnlyDictionary<string, byte[]> Load(string? eid = null) {
        var fixtures = ReadTier(Path.Combine(root, DefaultTier));
        if (eid is null)
            return fixtures;
        foreach (var (path, bytes) in ReadTier(Path.Combine(root, EidsTier, CheckEid(eid))))
            fixtures[path] = bytes;
        return fixtures;
    }

    public IReadOnlyDictionary<string, byte[]> LoadStaged() => ReadTier(Path.Combine(root, StagedTier));

    public IReadOnlyList<string> Eids() {
        var dir = Path.Combine(root, EidsTier);
        return Directory.Exists(dir)
            ? [.. Directory.EnumerateDirectories(dir).Select(d => Path.GetFileName(d)).Order(StringComparer.Ordinal)]
            : [];
    }

    public StubAuxbrainHandler Handler(string? eid = null) => new(Load(eid));

    private static string CheckEid(string eid) =>
        eid.Length == 0 || Path.GetFileName(eid) != eid || eid is "." or ".."
            ? throw new ArgumentException($"'{eid}' is not a fixture eid directory name.", nameof(eid))
            : eid;

    private static Dictionary<string, byte[]> ReadTier(string dir) {
        var fixtures = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (!Directory.Exists(dir))
            return fixtures;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            fixtures[Key(dir, file)] = File.ReadAllBytes(file);
        return fixtures;
    }

    private static string Key(string dir, string file) {
        var relative = Path.GetRelativePath(dir, file).Replace(Path.DirectorySeparatorChar, '/');
        var extension = Path.GetExtension(relative);
        return extension.Length == 0 ? relative : relative[..^extension.Length];
    }
}

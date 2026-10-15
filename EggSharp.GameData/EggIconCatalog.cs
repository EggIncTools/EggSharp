namespace EggSharp.GameData;

public sealed record EggIconEntry(
    int Index,
    string EggName,
    string IconStem);

public interface IEggIconCatalog {
    IReadOnlyList<EggIconEntry> EggIcons { get; }
    string BinaryVersion { get; }
    IReadOnlyDictionary<string, ProvenanceSource> Provenance { get; }
    EggIconEntry? Find(int index);
    EggIconEntry? Find(string eggName);
}

public sealed class EggIconCatalog(IReadOnlyList<EggIconEntry> eggIcons, string binaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource> provenance)
    : GameDataCatalog<EggIconEntry, int>(eggIcons, binaryVersion, provenance, e => e.Index), IEggIconCatalog {
    public const string DocumentId = "egg_icons";

    private readonly Dictionary<string, EggIconEntry> _byName =
        eggIcons.ToDictionary(e => e.EggName, StringComparer.Ordinal);

    public IReadOnlyList<EggIconEntry> EggIcons => Entries;
    public string BinaryVersion => Version;

    public EggIconEntry? Find(int index) => FindByKey(index);

    public EggIconEntry? Find(string eggName) => _byName.GetValueOrDefault(eggName);

    public static EggIconCatalog Parse(string json) {
        var file = GameDataJson.Deserialize<EggIconDataFile>(json, "Egg icon catalog");
        if (file.EggIcons is null)
            throw new GameDataSchemaException("Egg icon catalog missing eggIcons.");
        var icons = file.EggIcons.Select(ToEntry).ToArray();
        return new EggIconCatalog(icons, file.BinaryVersion ?? "", file.Provenance ?? GameData.Provenance.Empty);
    }

    private static EggIconEntry ToEntry(EggIconRow row) {
        if (row.Index is null)
            throw new GameDataSchemaException("Egg icon catalog row missing index.");
        if (string.IsNullOrEmpty(row.EggName))
            throw new GameDataSchemaException($"Egg icon catalog index {row.Index} missing eggName.");
        if (string.IsNullOrEmpty(row.IconStem))
            throw new GameDataSchemaException($"Egg icon catalog '{row.EggName}' missing iconStem.");
        return new EggIconEntry(row.Index.Value, row.EggName, row.IconStem);
    }
}

public sealed record EggIconRow(
    int? Index,
    string? EggName,
    string? IconStem);

public sealed record EggIconDataFile(
    string? BinaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource>? Provenance,
    IReadOnlyList<EggIconRow> EggIcons);

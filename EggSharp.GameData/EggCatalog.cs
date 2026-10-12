namespace EggSharp.GameData;

public sealed record EggCatalogEntry(
    int Index,
    string? Name,
    double BaseValue);

public interface IEggCatalog {
    IReadOnlyList<EggCatalogEntry> Eggs { get; }
    string BinaryVersion { get; }
    IReadOnlyDictionary<string, ProvenanceSource> Provenance { get; }
    EggCatalogEntry? Find(int index);
}

public sealed class EggCatalog(IReadOnlyList<EggCatalogEntry> eggs, string binaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource> provenance)
    : GameDataCatalog<EggCatalogEntry, int>(eggs, binaryVersion, provenance, e => e.Index), IEggCatalog {
    public IReadOnlyList<EggCatalogEntry> Eggs => Entries;
    public string BinaryVersion => Version;

    public EggCatalogEntry? Find(int index) => FindByKey(index);

    public static EggCatalog Parse(string json) {
        var file = GameDataJson.Deserialize<EggCatalogDataFile>(json, "Egg catalog");
        if (file.Eggs is null)
            throw new GameDataSchemaException("Egg catalog missing eggs.");
        var eggs = file.Eggs.Select(ToEntry).ToArray();
        return new EggCatalog(eggs, file.BinaryVersion ?? "", file.Provenance ?? GameData.Provenance.Empty);
    }

    private static EggCatalogEntry ToEntry(EggCatalogRow row) {
        if (row.Index is null)
            throw new GameDataSchemaException("Egg catalog row missing index.");
        if (row.BaseValue is null)
            throw new GameDataSchemaException($"Egg catalog index {row.Index} missing baseValue.");
        return new EggCatalogEntry(row.Index.Value, row.Name, row.BaseValue.Value);
    }
}

public sealed record EggCatalogRow(
    int? Index,
    string? Name,
    double? BaseValue);

public sealed record EggCatalogDataFile(
    string? BinaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource>? Provenance,
    IReadOnlyList<EggCatalogRow> Eggs);

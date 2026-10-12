namespace EggSharp.GameData;

public interface IDimensionCatalog {
    IReadOnlyList<string> Dimensions { get; }
    string BinaryVersion { get; }
    IReadOnlyDictionary<string, ProvenanceSource> Provenance { get; }
    bool Contains(string id);
}

public sealed class DimensionCatalog(IReadOnlyList<string> dimensions, string binaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource> provenance)
    : GameDataCatalog<string, string>(dimensions, binaryVersion, provenance, d => d, StringComparer.Ordinal), IDimensionCatalog {
    public IReadOnlyList<string> Dimensions => Entries;
    public string BinaryVersion => Version;

    public bool Contains(string id) => ContainsKey(id);

    public static DimensionCatalog Parse(string json) {
        var file = GameDataJson.Deserialize<DimensionCatalogDataFile>(json, "Dimension catalog");
        if (file.Dimensions is null)
            throw new GameDataSchemaException("Dimension catalog missing dimensions.");
        foreach (var id in file.Dimensions) {
            if (string.IsNullOrEmpty(id))
                throw new GameDataSchemaException("Dimension catalog contains an empty id.");
        }

        return new DimensionCatalog(file.Dimensions, file.BinaryVersion ?? "",
            file.Provenance ?? GameData.Provenance.Empty);
    }
}

public sealed record DimensionCatalogDataFile(
    string? BinaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource>? Provenance,
    IReadOnlyList<string> Dimensions);

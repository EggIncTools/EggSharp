namespace EggSharp.GameData;

public sealed record MissionCatalogEntry(
    string Id,
    string? DisplayName,
    string Goal);

public interface IMissionCatalog {
    IReadOnlyList<MissionCatalogEntry> Missions { get; }
    string BinaryVersion { get; }
    IReadOnlyDictionary<string, ProvenanceSource> Provenance { get; }
    MissionCatalogEntry? Find(string id);
}

public sealed class MissionCatalog(IReadOnlyList<MissionCatalogEntry> missions, string binaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource> provenance)
    : GameDataCatalog<MissionCatalogEntry, string>(missions, binaryVersion, provenance, m => m.Id, StringComparer.Ordinal), IMissionCatalog {
    public IReadOnlyList<MissionCatalogEntry> Missions => Entries;
    public string BinaryVersion => Version;

    public MissionCatalogEntry? Find(string id) => FindByKey(id);

    public static MissionCatalog Parse(string json) {
        var file = GameDataJson.Deserialize<MissionCatalogDataFile>(json, "Mission catalog");
        if (file.Missions is null)
            throw new GameDataSchemaException("Mission catalog missing missions.");
        var missions = file.Missions.Select(ToEntry).ToArray();
        return new MissionCatalog(missions, file.BinaryVersion ?? "", file.Provenance ?? GameData.Provenance.Empty);
    }

    private static MissionCatalogEntry ToEntry(MissionCatalogRow row) {
        if (string.IsNullOrEmpty(row.Id))
            throw new GameDataSchemaException("Mission catalog row missing id.");
        if (string.IsNullOrEmpty(row.Goal))
            throw new GameDataSchemaException($"Mission catalog '{row.Id}' missing goal.");
        return new MissionCatalogEntry(row.Id, row.DisplayName, row.Goal);
    }
}

public sealed record MissionCatalogRow(
    string? Id,
    string? DisplayName,
    string? Goal);

public sealed record MissionCatalogDataFile(
    string? BinaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource>? Provenance,
    IReadOnlyList<MissionCatalogRow> Missions);

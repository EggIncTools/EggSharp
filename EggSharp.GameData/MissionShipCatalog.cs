namespace EggSharp.GameData;

public sealed record MissionShipDuration(
    string Type,
    double Seconds,
    int Capacity,
    int LevelCapacityBump,
    double Quality,
    double MinQuality,
    double MaxQuality,
    double LevelQualityBump);

public sealed record MissionShipEntry(
    string Ship,
    IReadOnlyList<int> LevelMissionRequirements,
    IReadOnlyList<MissionShipDuration> Durations) {
    public MissionShipDuration? Duration(string type) =>
        Durations.FirstOrDefault(d => string.Equals(d.Type, type, StringComparison.Ordinal));
}

public interface IMissionShipCatalog {
    IReadOnlyList<MissionShipEntry> Ships { get; }
    string BinaryVersion { get; }
    IReadOnlyDictionary<string, ProvenanceSource> Provenance { get; }
    MissionShipEntry? Find(string ship);
}

public sealed class MissionShipCatalog(IReadOnlyList<MissionShipEntry> ships, string binaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource> provenance)
    : GameDataCatalog<MissionShipEntry, string>(ships, binaryVersion, provenance, s => s.Ship, StringComparer.Ordinal), IMissionShipCatalog {
    public const string DocumentId = "mission-ships";

    public IReadOnlyList<MissionShipEntry> Ships => Entries;
    public string BinaryVersion => Version;

    public MissionShipEntry? Find(string ship) => FindByKey(ship);

    public static MissionShipCatalog Parse(string json) {
        var file = GameDataJson.Deserialize<MissionShipDataFile>(json, "Mission ship catalog");
        if (file.Ships is null)
            throw new GameDataSchemaException("Mission ship catalog missing ships.");
        var ships = file.Ships.Select(ToEntry).ToArray();
        return new MissionShipCatalog(ships, file.BinaryVersion ?? "", file.Provenance ?? GameData.Provenance.Empty);
    }

    private static MissionShipEntry ToEntry(MissionShipRow row) {
        if (string.IsNullOrEmpty(row.Ship))
            throw new GameDataSchemaException("Mission ship catalog row missing ship.");
        if (row.Durations is null or { Count: 0 })
            throw new GameDataSchemaException($"Mission ship catalog '{row.Ship}' missing durations.");
        var durations = row.Durations.Select(d => ToDuration(row.Ship, d)).ToArray();
        var duplicate = durations.GroupBy(d => d.Type, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new GameDataSchemaException($"Mission ship catalog '{row.Ship}' has duplicate duration '{duplicate.Key}'.");
        return new MissionShipEntry(row.Ship, row.LevelMissionRequirements ?? [], durations);
    }

    private static MissionShipDuration ToDuration(string ship, MissionShipDurationRow row) {
        if (string.IsNullOrEmpty(row.Type))
            throw new GameDataSchemaException($"Mission ship catalog '{ship}' duration missing type.");
        if (row.Seconds is null or <= 0)
            throw new GameDataSchemaException($"Mission ship catalog '{ship}' duration '{row.Type}' missing seconds.");
        if (row.Capacity is null or <= 0)
            throw new GameDataSchemaException($"Mission ship catalog '{ship}' duration '{row.Type}' missing capacity.");
        return new MissionShipDuration(row.Type, row.Seconds.Value, row.Capacity.Value, row.LevelCapacityBump ?? 0,
            row.Quality ?? 0, row.MinQuality ?? 0, row.MaxQuality ?? 0, row.LevelQualityBump ?? 0);
    }
}

public sealed record MissionShipDurationRow(
    string? Type,
    double? Seconds,
    int? Capacity,
    int? LevelCapacityBump,
    double? Quality,
    double? MinQuality,
    double? MaxQuality,
    double? LevelQualityBump);

public sealed record MissionShipRow(
    string? Ship,
    IReadOnlyList<int>? LevelMissionRequirements,
    IReadOnlyList<MissionShipDurationRow>? Durations);

public sealed record MissionShipDataFile(
    string? BinaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource>? Provenance,
    IReadOnlyList<MissionShipRow> Ships);

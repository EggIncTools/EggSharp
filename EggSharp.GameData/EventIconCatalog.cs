namespace EggSharp.GameData;

public sealed record EventIconEntry(
    int Index,
    string EventType,
    string IconStem);

public interface IEventIconCatalog {
    IReadOnlyList<EventIconEntry> EventIcons { get; }
    string BinaryVersion { get; }
    IReadOnlyDictionary<string, ProvenanceSource> Provenance { get; }
    EventIconEntry? Find(int index);
    EventIconEntry? Find(string eventType);
}

public sealed class EventIconCatalog(IReadOnlyList<EventIconEntry> eventIcons, string binaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource> provenance)
    : GameDataCatalog<EventIconEntry, int>(eventIcons, binaryVersion, provenance, e => e.Index), IEventIconCatalog {
    public const string DocumentId = "event_icons";

    private readonly Dictionary<string, EventIconEntry> _byType =
        eventIcons.ToDictionary(e => e.EventType, StringComparer.Ordinal);

    public IReadOnlyList<EventIconEntry> EventIcons => Entries;
    public string BinaryVersion => Version;

    public EventIconEntry? Find(int index) => FindByKey(index);

    public EventIconEntry? Find(string eventType) => _byType.GetValueOrDefault(eventType);

    public static EventIconCatalog Parse(string json) {
        var file = GameDataJson.Deserialize<EventIconDataFile>(json, "Event icon catalog");
        if (file.EventIcons is null)
            throw new GameDataSchemaException("Event icon catalog missing eventIcons.");
        var icons = file.EventIcons.Select(ToEntry).ToArray();
        return new EventIconCatalog(icons, file.BinaryVersion ?? "", file.Provenance ?? GameData.Provenance.Empty);
    }

    private static EventIconEntry ToEntry(EventIconRow row) {
        if (row.Index is null)
            throw new GameDataSchemaException("Event icon catalog row missing index.");
        if (string.IsNullOrEmpty(row.EventType))
            throw new GameDataSchemaException($"Event icon catalog index {row.Index} missing eventType.");
        if (string.IsNullOrEmpty(row.IconStem))
            throw new GameDataSchemaException($"Event icon catalog '{row.EventType}' missing iconStem.");
        return new EventIconEntry(row.Index.Value, row.EventType, row.IconStem);
    }
}

public sealed record EventIconRow(
    int? Index,
    string? EventType,
    string? IconStem);

public sealed record EventIconDataFile(
    string? BinaryVersion,
    IReadOnlyDictionary<string, ProvenanceSource>? Provenance,
    IReadOnlyList<EventIconRow> EventIcons);

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace EggSharp.GameData;

public sealed class GameDataProvider(
    IReadOnlyList<IEffectFamily> families,
    IColleggtibleCatalog colleggtibles,
    IBoostCatalog boostCatalog,
    IEggCatalog eggCatalog,
    IDimensionCatalog dimensions,
    IMissionCatalog missions,
    IVehicleCatalog vehicles) : IGameDataProvider {
    private const string BoostsId = "boosts";

    private const string ArtifactsId = "artifacts";

    private const string EmptyEffectData = """{"binaryVersion":"none","rows":[]}""";

    private const string EmbeddedDocumentPrefix = "game-data/";

    private const string EmbeddedDocumentSuffix = ".json";

    public static readonly ImmutableArray<string> DocumentIds = [
        BoostsId,
        "research",
        "habs",
        ArtifactsId,
        "boost-catalog",
        "colleggtibles",
        "eggs",
        "dimensions",
        "missions",
        "vehicles"
    ];

    public static readonly ImmutableArray<string> AuxiliaryDocumentIds = ["farm-placement"];

    public static readonly ImmutableArray<string> OptionalAuxiliaryDocumentIds =
        [ArtifactCatalog.DocumentId, MissionShipCatalog.DocumentId];

    public static readonly ImmutableArray<string> OptionalDocumentIds = [BoostsId, ArtifactsId];

    public static readonly ImmutableArray<string> RequiredDocumentIds =
        [.. DocumentIds.Where(id => !OptionalDocumentIds.Contains(id)), .. AuxiliaryDocumentIds];

    public static readonly ImmutableArray<string> ImportableIds =
        [.. DocumentIds, .. AuxiliaryDocumentIds, .. OptionalAuxiliaryDocumentIds];

    private readonly Dictionary<string, IEffectFamily> _byKey =
        families.ToDictionary(f => f.Key, StringComparer.Ordinal);

    public IReadOnlyList<IEffectFamily> Families { get; } = families;
    public IColleggtibleCatalog Colleggtibles { get; } = colleggtibles;
    public IBoostCatalog BoostCatalog { get; } = boostCatalog;
    public IEggCatalog EggCatalog { get; } = eggCatalog;
    public IDimensionCatalog Dimensions { get; } = dimensions;
    public IMissionCatalog Missions { get; } = missions;
    public IVehicleCatalog Vehicles { get; } = vehicles;

    public IEffectFamily? Family(string key) => _byKey.GetValueOrDefault(key);

    public Effect? Resolve(string family, string id) => Family(family)?.Find(id);

    public bool TryResolve(string family, string id, [NotNullWhen(true)] out Effect? effect) {
        effect = Resolve(family, id);
        return effect is not null;
    }

    public IReadOnlyList<Effect> All(string family) =>
        Family(family)?.Effects ?? [];

    public IReadOnlyList<Effect> ByTarget(EffectTarget target) =>
        Families.SelectMany(f => f.Effects).Where(e => e.Target == target).ToArray();

    public double Effective(EffectTarget target, double seed, IReadOnlyDictionary<string, int> idLevels) {
        var active = new List<Effect>();
        foreach (var id in idLevels.Keys) {
            foreach (var family in Families) {
                var effect = family.Find(id);
                if (effect is not null && effect.Target == target)
                    active.Add(effect);
            }
        }

        var value = seed;
        foreach (var group in active.GroupBy(e => e.CombineMode)) {
            var contributions = group.Select(e => e.Contribution(LevelOf(idLevels, e.Id)));
            value = Folding.Fold(group.Key, value, contributions);
        }

        return value;
    }

    public static GameDataProvider CreateDefault() => FromDocuments(EmbeddedDocuments());

    public static IReadOnlyDictionary<string, string> EmbeddedDocuments() {
        var assembly = typeof(GameDataProvider).Assembly;
        var docs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames()) {
            if (!name.StartsWith(EmbeddedDocumentPrefix, StringComparison.Ordinal)
                || !name.EndsWith(EmbeddedDocumentSuffix, StringComparison.Ordinal)) {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            docs[name[EmbeddedDocumentPrefix.Length..^EmbeddedDocumentSuffix.Length]] = reader.ReadToEnd();
        }

        return docs;
    }

    public static GameDataProvider FromDocuments(IReadOnlyDictionary<string, string> docs) =>
        new([
                new BoostFamily(EffectDataLoader.Parse(OptionalDoc(docs, BoostsId))),
                new ResearchFamily(EffectDataLoader.Parse(Doc(docs, "research"))),
                new HabFamily(EffectDataLoader.Parse(Doc(docs, "habs"))),
                new ArtifactFamily(EffectDataLoader.Parse(OptionalDoc(docs, ArtifactsId)))
            ], ColleggtibleCatalog.Parse(Doc(docs, "colleggtibles")),
            GameData.BoostCatalog.Parse(Doc(docs, "boost-catalog")),
            GameData.EggCatalog.Parse(Doc(docs, "eggs")),
            DimensionCatalog.Parse(Doc(docs, "dimensions")),
            MissionCatalog.Parse(Doc(docs, "missions")),
            VehicleCatalog.Parse(Doc(docs, "vehicles")));

    public static GameDataProvider Verify(IReadOnlyDictionary<string, string> docs) {
        ArgumentNullException.ThrowIfNull(docs);
        foreach (var (id, json) in docs)
            Validate(id, json);
        return FromDocuments(docs);
    }

    public static void Validate(string id, string json) {
        _ = (object)(id switch {
            BoostsId => new BoostFamily(EffectDataLoader.Parse(json)),
            "research" => new ResearchFamily(EffectDataLoader.Parse(json)),
            "habs" => new HabFamily(EffectDataLoader.Parse(json)),
            ArtifactsId => new ArtifactFamily(EffectDataLoader.Parse(json)),
            "boost-catalog" => GameData.BoostCatalog.Parse(json),
            "colleggtibles" => ColleggtibleCatalog.Parse(json),
            "eggs" => GameData.EggCatalog.Parse(json),
            "dimensions" => DimensionCatalog.Parse(json),
            "missions" => MissionCatalog.Parse(json),
            "vehicles" => VehicleCatalog.Parse(json),
            FarmPlacementCatalog.DocumentId => FarmPlacementCatalog.Parse(json),
            ArtifactCatalog.DocumentId => ArtifactCatalog.Parse(json),
            MissionShipCatalog.DocumentId => MissionShipCatalog.Parse(json),
            _ => throw new GameDataSchemaException($"Unknown game data document id '{id}'.")
        });
    }

    private static string OptionalDoc(IReadOnlyDictionary<string, string> docs, string id) =>
        docs.TryGetValue(id, out var json) ? json : EmptyEffectData;

    private static string Doc(IReadOnlyDictionary<string, string> docs, string id) =>
        docs.TryGetValue(id, out var json)
            ? json
            : throw new GameDataSchemaException($"Missing game data document '{id}'.");

    private static int LevelOf(IReadOnlyDictionary<string, int> idLevels, string id) =>
        idLevels.GetValueOrDefault(id, 1);
}

using System.Text.Json.Serialization;

namespace EggSharp.GameData.Artifacts;

public sealed record ArtifactFamily(
    string Name,
    int AfxId,
    string BinaryId,
    string PluralName,
    ArtifactKind Kind,
    int Dimension,
    int Order,
    string Description,
    IReadOnlyList<ArtifactTier> Tiers) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }
}

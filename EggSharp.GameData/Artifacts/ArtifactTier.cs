namespace EggSharp.GameData.Artifacts;

public sealed record ArtifactTier(
    int Level,
    string Name,
    int RarityCount,
    IReadOnlyList<ArtifactIngredient> Recipe,
    IReadOnlyList<ArtifactEffect> Effects);

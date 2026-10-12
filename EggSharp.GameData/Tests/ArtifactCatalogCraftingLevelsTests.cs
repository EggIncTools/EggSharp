namespace EggSharp.GameData.Tests;

public sealed class ArtifactCatalogCraftingLevelsTests {
    private const string Document = """
        {
          "binaryVersion": "test-1.0",
          "provenance": {},
          "artifacts": [
            {
              "id": "lunar-totem-1-common", "specName": "LUNAR_TOTEM", "level": "INFERIOR", "rarity": "COMMON",
              "afxId": 0, "afxLevel": 0, "afxRarity": 0, "tierNumber": 1, "baseQuality": 0.7, "oddsMultiplier": 0.9,
              "value": 58.6, "craftingPrice": 10.8, "craftingPriceLow": 1.08, "craftingPriceCurve": 0.2,
              "craftingPriceDomain": 300, "craftingXp": 1
            }
          ],
          "craftingLevels": [
            { "level": 1, "xpRequired": 0, "rarityMult": 1 },
            { "level": 2, "xpRequired": 500, "rarityMult": 1.25 },
            { "level": 3, "xpRequired": 2000, "rarityMult": 1.5 }
          ]
        }
        """;

    [Fact]
    public void CraftingLevels_RoundTripThroughTheDocument() {
        var parsed = ArtifactCatalog.Parse(Document);

        Assert.Equal(3, parsed.CraftingLevels.Count);
        Assert.Equal(new[] { 1.0, 1.25, 1.5 }, parsed.CraftingLevels.Select(l => l.RarityMult));
        Assert.Equal(2000, parsed.CraftingLevels[2].XpRequired);
    }

    [Fact]
    public void CraftingLevels_AbsentInDocument_ParsesEmpty() {
        var parsed = ArtifactCatalog.Parse(Document.Replace("\"craftingLevels\"", "\"unusedLevels\"", StringComparison.Ordinal));

        Assert.Empty(parsed.CraftingLevels);
    }
}

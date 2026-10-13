namespace EggSharp.GameData.Tests;

public sealed class MissionShipCatalogTests {
    private const string Document = """
        {
          "binaryVersion": "test-1.0",
          "provenance": {},
          "ships": [
            {
              "ship": "CHICKEN_ONE",
              "levelMissionRequirements": [4, 6, 12],
              "durations": [
                { "type": "SHORT", "seconds": 1200.0, "capacity": 4, "levelCapacityBump": 1, "quality": 1.0, "minQuality": 0.0, "maxQuality": 1.4, "levelQualityBump": 0 },
                { "type": "LONG", "seconds": 3600.0, "capacity": 5, "levelCapacityBump": 1, "quality": 1.2, "minQuality": 0.0, "maxQuality": 1.8, "levelQualityBump": 0 },
                { "type": "EPIC", "seconds": 14400.0, "capacity": 6, "levelCapacityBump": 2, "quality": 1.5, "minQuality": 0.5, "maxQuality": 2.5, "levelQualityBump": 0.05 }
              ]
            },
            {
              "ship": "CHICKEN_NINE",
              "durations": [
                { "type": "SHORT", "seconds": 1800, "capacity": 9 }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Duration_RoundTripsThroughTheDocument() {
        var epic = MissionShipCatalog.Parse(Document).Ships[0].Duration("EPIC");

        Assert.NotNull(epic);
        Assert.Equal(14400, epic.Seconds);
        Assert.Equal(6, epic.Capacity);
        Assert.Equal(2, epic.LevelCapacityBump);
        Assert.Equal(1.5, epic.Quality);
        Assert.Equal(0.5, epic.MinQuality);
        Assert.Equal(2.5, epic.MaxQuality);
        Assert.Equal(0.05, epic.LevelQualityBump);
    }

    [Fact]
    public void Find_ReturnsShipByName() {
        var catalog = MissionShipCatalog.Parse(Document);

        var nine = catalog.Find("CHICKEN_NINE");
        Assert.NotNull(nine);
        Assert.Equal(1800, nine.Duration("SHORT")!.Seconds);
        Assert.Empty(nine.LevelMissionRequirements);
        Assert.Equal(new[] { 4, 6, 12 }, catalog.Find("CHICKEN_ONE")!.LevelMissionRequirements);
        Assert.Null(catalog.Find("chicken_one"));
    }

    [Fact]
    public void Parse_MissingShipName_Throws() {
        var json = Document.Replace("\"ship\": \"CHICKEN_NINE\",", "", StringComparison.Ordinal);

        Assert.Throws<GameDataSchemaException>(() => MissionShipCatalog.Parse(json));
    }

    [Fact]
    public void Parse_DuplicateDurationType_Throws() {
        var json = Document.Replace("\"type\": \"LONG\"", "\"type\": \"SHORT\"", StringComparison.Ordinal);

        Assert.Throws<GameDataSchemaException>(() => MissionShipCatalog.Parse(json));
    }

    [Fact]
    public void Validate_AcceptsMissionShipDocument() {
        GameDataProvider.Validate(MissionShipCatalog.DocumentId, Document);
    }
}

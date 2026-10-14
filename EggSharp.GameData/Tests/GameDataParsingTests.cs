namespace EggSharp.GameData.Tests;

public sealed class GameDataParsingTests {
    private const string BoostsJson = """
        {
          "binaryVersion": "test-1.0",
          "rows": [
            { "id": "boost_a", "target": "IHR", "combineMode": "Mul", "magnitude": 10, "meta": { "kind": "InternalHatcheryMult", "durationSeconds": 600 } },
            { "id": "beacon_a", "target": "BeaconMult", "combineMode": "Add", "magnitude": 1, "meta": { "kind": "BeaconMult", "durationSeconds": 600 } }
          ]
        }
        """;

    private const string ResearchJson = """
        {
          "binaryVersion": "test-1.0",
          "rows": [
            { "id": "res_a", "target": "IHR", "combineMode": "MulPlusOne", "magnitude": 0.05, "maxLevel": 10, "meta": { "name": "Test Research", "epic": false, "tier": 1, "priceStart": 100, "priceEnd": 5000, "pricePercent": 0.1, "epicPriceStart": 0, "epicPriceEnd": 0 } }
          ]
        }
        """;

    private const string HabsJson = """
        {
          "binaryVersion": "test-1.0",
          "rows": [
            { "id": "hab_a", "target": "HabCapacity", "combineMode": "Add", "magnitude": 100, "meta": { "habId": 1 } }
          ]
        }
        """;

    private const string ArtifactsJson = """
        {
          "binaryVersion": "test-1.0",
          "rows": [
            { "id": "art_a", "target": "IHR", "combineMode": "MulPlusOne", "magnitude": 0.1, "meta": { "boost": "boost_a" } }
          ]
        }
        """;

    private const string BoostCatalogJson = """
        {
          "binaryVersion": "test-1.0",
          "boosts": [
            { "id": "boost_a", "displayName": "Boost A", "price": 200, "tokenPrice": 1, "iconAsset": "b_icon_a" }
          ]
        }
        """;

    private const string ColleggtiblesJson = """
        {
          "gameVersion": "test-1.0",
          "eggs": [
            { "identifier": "carbon-fiber", "dimension": "SHIPPING_CAPACITY", "tierValues": [1.01, 1.02, 1.03, 1.05] }
          ],
          "contractEggMap": { "contract-a": "carbon-fiber" }
        }
        """;

    private const string EggsJson = """
        {
          "binaryVersion": "test-1.0",
          "eggs": [
            { "index": 1, "name": "Edible", "baseValue": 0.1 }
          ]
        }
        """;

    private const string DimensionsJson = """
        {
          "binaryVersion": "test-1.0",
          "dimensions": [ "EARNINGS", "SHIPPING_CAPACITY" ]
        }
        """;

    private const string MissionsJson = """
        {
          "binaryVersion": "test-1.0",
          "missions": [
            { "id": "mission_a", "displayName": "Mission A", "goal": "collect" }
          ]
        }
        """;

    private const string VehiclesJson = """
        {
          "binaryVersion": "test-1.0",
          "vehicles": [
            { "index": 0, "name": "Trike", "capacity": 5000 }
          ]
        }
        """;

    private static Dictionary<string, string> AllDocs() => new(StringComparer.Ordinal) {
        ["boosts"] = BoostsJson,
        ["research"] = ResearchJson,
        ["habs"] = HabsJson,
        ["artifacts"] = ArtifactsJson,
        ["boost-catalog"] = BoostCatalogJson,
        ["colleggtibles"] = ColleggtiblesJson,
        ["eggs"] = EggsJson,
        ["dimensions"] = DimensionsJson,
        ["missions"] = MissionsJson,
        ["vehicles"] = VehiclesJson
    };

    [Fact]
    public void FromDocuments_builds_provider_from_all_ten_documents() {
        var provider = GameDataProvider.FromDocuments(AllDocs());
        Assert.Equal(4, provider.Families.Count);
        Assert.NotNull(provider.Resolve(Families.Boost, "boost_a"));
        Assert.NotNull(provider.BoostCatalog.Find("boost_a"));
        Assert.NotNull(provider.Colleggtibles.Find("carbon-fiber"));
        Assert.NotNull(provider.EggCatalog.Find(1));
        Assert.True(provider.Dimensions.Contains("EARNINGS"));
        Assert.NotNull(provider.Missions.Find("mission_a"));
        Assert.NotNull(provider.Vehicles.Find(0));
    }

    [Fact]
    public void FromDocuments_missing_document_throws_naming_the_id() {
        var docs = AllDocs();
        docs.Remove("missions");
        var ex = Assert.Throws<GameDataSchemaException>(() => GameDataProvider.FromDocuments(docs));
        Assert.Contains("missions", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentIds_lists_all_ten_ids() {
        Assert.Equal(10, GameDataProvider.DocumentIds.Length);
        var docs = AllDocs();
        foreach (var id in GameDataProvider.DocumentIds)
            Assert.True(docs.ContainsKey(id), id);
    }

    [Fact]
    public void Validate_accepts_every_synthetic_document() {
        foreach (var (id, json) in AllDocs())
            GameDataProvider.Validate(id, json);
    }

    [Fact]
    public void Verify_accepts_the_shipped_documents() =>
        Assert.NotNull(GameDataProvider.Verify(GameDataProvider.EmbeddedDocuments()));

    [Fact]
    public void Verify_rejects_an_invalid_document_even_when_the_fold_would_skip_it() {
        var docs = new Dictionary<string, string>(GameDataProvider.EmbeddedDocuments()) {
            [FarmPlacementCatalog.DocumentId] = "not json"
        };
        Assert.Throws<GameDataSchemaException>(() => GameDataProvider.Verify(docs));
    }

    [Fact]
    public void Verify_rejects_a_missing_required_document() {
        var docs = new Dictionary<string, string>(GameDataProvider.EmbeddedDocuments());
        docs.Remove("missions");
        Assert.Throws<GameDataSchemaException>(() => GameDataProvider.Verify(docs));
    }

    [Fact]
    public void Verify_rejects_a_catalog_boost_without_a_boosts_row() {
        var docs = new Dictionary<string, string>(GameDataProvider.EmbeddedDocuments()) {
            ["boosts"] = """{"binaryVersion":"1","rows":[]}"""
        };
        var ex = Assert.Throws<GameDataSchemaException>(() => GameDataProvider.Verify(docs));
        Assert.Contains("quantum_bulb", ex.Message);
    }

    [Fact]
    public void Validate_rejects_invalid_json() =>
        Assert.Throws<GameDataSchemaException>(() => GameDataProvider.Validate("eggs", "not json"));

    [Fact]
    public void Validate_rejects_unknown_id() {
        var ex = Assert.Throws<GameDataSchemaException>(() => GameDataProvider.Validate("nope", "{}"));
        Assert.Contains("nope", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Effective_folds_synthetic_magnitudes_across_families() {
        var provider = GameDataProvider.FromDocuments(AllDocs());
        var value = provider.Effective(EffectTarget.IHR, 1, new Dictionary<string, int> {
            ["boost_a"] = 1,
            ["res_a"] = 2
        });
        Assert.Equal(10 * (1 + 0.1), value, 10);
    }

    [Fact]
    public void ColleggtibleCatalog_requires_exactly_four_tier_values() {
        const string json = """
            {
              "gameVersion": "test-1.0",
              "eggs": [
                { "identifier": "short", "dimension": "EARNINGS", "tierValues": [1.01, 1.02, 1.03] }
              ]
            }
            """;
        var ex = Assert.Throws<GameDataSchemaException>(() => ColleggtibleCatalog.Parse(json));
        Assert.Contains("4 tierValues", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DimensionCodes_map_is_intact() {
        Assert.Equal(10, ColleggtibleCatalog.DimensionCodes.Count);
        Assert.Equal(0, ColleggtibleCatalog.DimensionCodes["INVALID"]);
        Assert.Equal(1, ColleggtibleCatalog.DimensionCodes["EARNINGS"]);
        Assert.Equal(2, ColleggtibleCatalog.DimensionCodes["AWAY_EARNINGS"]);
        Assert.Equal(3, ColleggtibleCatalog.DimensionCodes["INTERNAL_HATCHERY_RATE"]);
        Assert.Equal(4, ColleggtibleCatalog.DimensionCodes["EGG_LAYING_RATE"]);
        Assert.Equal(5, ColleggtibleCatalog.DimensionCodes["SHIPPING_CAPACITY"]);
        Assert.Equal(6, ColleggtibleCatalog.DimensionCodes["HAB_CAPACITY"]);
        Assert.Equal(7, ColleggtibleCatalog.DimensionCodes["VEHICLE_COST"]);
        Assert.Equal(8, ColleggtibleCatalog.DimensionCodes["HAB_COST"]);
        Assert.Equal(9, ColleggtibleCatalog.DimensionCodes["RESEARCH_COST"]);
    }

    [Fact]
    public void ImportableIds_extends_DocumentIds_without_gating_the_provider() {
        foreach (var id in GameDataProvider.DocumentIds)
            Assert.Contains(id, GameDataProvider.ImportableIds);
        foreach (var id in GameDataProvider.AuxiliaryDocumentIds) {
            Assert.Contains(id, GameDataProvider.ImportableIds);
            Assert.DoesNotContain(id, GameDataProvider.DocumentIds);
        }

        foreach (var id in GameDataProvider.OptionalAuxiliaryDocumentIds) {
            Assert.Contains(id, GameDataProvider.ImportableIds);
            Assert.DoesNotContain(id, GameDataProvider.DocumentIds);
            Assert.DoesNotContain(id, GameDataProvider.RequiredDocumentIds);
        }

        Assert.Equal(
            GameDataProvider.DocumentIds.Length + GameDataProvider.AuxiliaryDocumentIds.Length +
            GameDataProvider.OptionalAuxiliaryDocumentIds.Length,
            GameDataProvider.ImportableIds.Length);
    }

    [Fact]
    public void Family_parses_rows_and_exposes_meta() {
        var family = new BoostFamily(EffectDataLoader.Parse(BoostsJson));
        Assert.Equal(2, family.Effects.Count);
        Assert.Equal("test-1.0", family.BinaryVersion);
        var effect = family.Find("boost_a");
        Assert.NotNull(effect);
        Assert.Equal(600, effect.MetaInt("durationSeconds"));
        Assert.Equal("InternalHatcheryMult", effect.MetaString("kind"));
    }

    [Fact]
    public void Family_row_missing_required_meta_field_throws() {
        const string json = """
            {
              "binaryVersion": "test-1.0",
              "rows": [
                { "id": "boost_bad", "target": "IHR", "combineMode": "Mul", "magnitude": 10, "meta": { "kind": "InternalHatcheryMult" } }
              ]
            }
            """;
        var ex = Assert.Throws<GameDataSchemaException>(() => new BoostFamily(EffectDataLoader.Parse(json)));
        Assert.Contains("durationSeconds", ex.Message, StringComparison.Ordinal);
    }

    private static ResearchFamily ResearchWith(string maxLevel, string prices) =>
        new(EffectDataLoader.Parse($$"""
            {
              "binaryVersion": "test-1.0",
              "rows": [
                { "id": "res_p", "target": "IHR", "combineMode": "MulPlusOne", "magnitude": 0.05{{maxLevel}}, "meta": { "epic": false, "prices": {{prices}} } }
              ]
            }
            """));

    [Fact]
    public void Research_prices_parse_and_Price_reads_one_based_levels() {
        var research = ResearchWith(""", "maxLevel": 3""", "[100, 250.5, 1000]").Find("res_p");
        Assert.NotNull(research);
        Assert.Equal([100, 250.5, 1000], research.MetaDoubles("prices"));
        Assert.Equal(100, ResearchFamily.Price(research, 1));
        Assert.Equal(1000, ResearchFamily.Price(research, 3));
        Assert.Null(ResearchFamily.Price(research, 0));
        Assert.Null(ResearchFamily.Price(research, 4));
    }

    [Fact]
    public void Research_Price_is_null_without_prices() {
        var research = new ResearchFamily(EffectDataLoader.Parse(ResearchJson)).Find("res_a");
        Assert.NotNull(research);
        Assert.Null(ResearchFamily.Price(research, 1));
    }

    [Fact]
    public void Research_prices_length_mismatch_throws_naming_the_row() {
        var ex = Assert.Throws<GameDataSchemaException>(() => ResearchWith(""", "maxLevel": 2""", "[100, 250, 1000]"));
        Assert.Contains("res_p", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Research_prices_without_maxLevel_throws_naming_the_row() {
        var ex = Assert.Throws<GameDataSchemaException>(() => ResearchWith("", "[100, 250, 1000]"));
        Assert.Contains("res_p", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Research_prices_with_non_numeric_element_throws() =>
        Assert.Throws<GameDataSchemaException>(() => ResearchWith(""", "maxLevel": 2""", """[100, "x"]"""));

    [Fact]
    public void EggCatalog_rejects_invalid_json() =>
        Assert.Throws<GameDataSchemaException>(() => EggCatalog.Parse("not json"));
}

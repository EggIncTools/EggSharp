namespace EggSharp.GameData.Farm;

public enum PlacementOrigin {
    Binary,
    Config,
    Fixture,
    Derived,
    Authored
}

public sealed record PlacementProvenance(PlacementOrigin Origin, string? Locator = null, string? Method = null) {
    public static PlacementProvenance FromBinary(string locator, string method = "decoded") =>
        new(PlacementOrigin.Binary, locator, method);

    public static PlacementProvenance Derived(string method) => new(PlacementOrigin.Derived, null, method);

    public static PlacementProvenance Authored(string method) => new(PlacementOrigin.Authored, null, method);
}

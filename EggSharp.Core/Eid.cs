namespace EggSharp.Core;

public readonly record struct Eid {
    public string Value { get; private init; }

    public static bool TryParse(string? text, out Eid eid) {
        eid = default;
        if (text is not { Length: 18 } || !text.StartsWith("EI", StringComparison.Ordinal))
            return false;
        foreach (var c in text.AsSpan(2)) {
            if (!char.IsAsciiDigit(c))
                return false;
        }
        eid = new Eid { Value = text };
        return true;
    }

    public static Eid Parse(string text) =>
        TryParse(text, out var eid) ? eid : throw new FormatException($"'{text}' is not an EID: EI followed by 16 digits.");

    public override string ToString() => Value;
}

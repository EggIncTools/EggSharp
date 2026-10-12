namespace EggSharp.ProtoParity;

public static class ProtoParser {
    private static readonly HashSet<string> Scalars = [
        "double", "float", "int32", "int64", "uint32", "uint64", "sint32", "sint64",
        "fixed32", "fixed64", "sfixed32", "sfixed64", "bool", "string", "bytes",
    ];

    private static readonly HashSet<string> SkippedStatements = ["syntax", "import", "option", "reserved", "extensions", "edition"];

    private static readonly HashSet<string> Labels = ["optional", "required", "repeated"];

    public static ProtoSchema Parse(string text) {
        var reader = new TokenReader(ProtoTokenizer.Tokenize(text));
        var package = "";
        var raw = new List<RawMessage>();
        var enums = new Dictionary<string, ProtoEnum>(StringComparer.Ordinal);
        while (!reader.AtEnd) {
            var token = reader.Next();
            switch (token) {
                case "package":
                    package = reader.Next();
                    reader.Expect(";");
                    break;
                case "message":
                    _ = ParseMessage(reader, package, raw, enums);
                    break;
                case "enum":
                    ParseEnum(reader, package, enums);
                    break;
                case "service" or "extend":
                    reader.SkipBlock();
                    break;
                case ";":
                    break;
                default:
                    if (!SkippedStatements.Contains(token))
                        throw new FormatException($"Unexpected token '{token}' at top level.");
                    reader.SkipStatement();
                    break;
            }
        }
        var known = new HashSet<string>(raw.Select(m => m.FullName).Concat(enums.Keys), StringComparer.Ordinal);
        var messages = raw.ToDictionary(m => m.FullName, m => Resolve(m, known), StringComparer.Ordinal);
        return new ProtoSchema(package, messages, enums);
    }

    private static string Qualify(string scope, string name) => scope.Length == 0 ? name : $"{scope}.{name}";

    private static string ParseMessage(TokenReader reader, string scope, List<RawMessage> messages, Dictionary<string, ProtoEnum> enums) {
        var name = reader.Next();
        var fullName = Qualify(scope, name);
        var message = new RawMessage(fullName, name);
        messages.Add(message);
        reader.Expect("{");
        while (!reader.TryConsume("}")) {
            var token = reader.Next();
            switch (token) {
                case "message":
                    message.Nested.Add(ParseMessage(reader, fullName, messages, enums));
                    break;
                case "enum":
                    message.Nested.Add(ParseEnum(reader, fullName, enums).FullName);
                    break;
                case "oneof":
                    ParseOneof(reader, message);
                    break;
                case "extend":
                    reader.SkipBlock();
                    break;
                case ";":
                    break;
                default:
                    if (SkippedStatements.Contains(token))
                        reader.SkipStatement();
                    else
                        message.Fields.Add(ParseField(reader, token, null));
                    break;
            }
        }
        return fullName;
    }

    private static void ParseOneof(TokenReader reader, RawMessage message) {
        var name = reader.Next();
        message.Oneofs.Add(name);
        reader.Expect("{");
        while (!reader.TryConsume("}")) {
            var token = reader.Next();
            if (token == ";")
                continue;
            if (token == "option")
                reader.SkipStatement();
            else
                message.Fields.Add(ParseField(reader, token, name));
        }
    }

    private static RawField ParseField(TokenReader reader, string first, string? oneof) {
        var repeated = first == "repeated";
        var type = Labels.Contains(first) ? reader.Next() : first;
        if (type == "map") {
            reader.Expect("<");
            var key = reader.Next();
            reader.Expect(",");
            var value = reader.Next();
            reader.Expect(">");
            type = $"map<{key},{value}>";
        }
        var name = reader.Next();
        reader.Expect("=");
        var number = int.Parse(reader.Next(), System.Globalization.CultureInfo.InvariantCulture);
        if (reader.TryConsume("["))
            reader.SkipBracketed("[", "]");
        reader.Expect(";");
        return new RawField(name, number, repeated, type, oneof);
    }

    private static ProtoEnum ParseEnum(TokenReader reader, string scope, Dictionary<string, ProtoEnum> enums) {
        var name = reader.Next();
        var values = new List<ProtoEnumValue>();
        reader.Expect("{");
        while (!reader.TryConsume("}")) {
            var token = reader.Next();
            if (token == ";")
                continue;
            if (SkippedStatements.Contains(token)) {
                reader.SkipStatement();
                continue;
            }
            reader.Expect("=");
            var number = int.Parse(reader.Next(), System.Globalization.CultureInfo.InvariantCulture);
            if (reader.TryConsume("["))
                reader.SkipBracketed("[", "]");
            reader.Expect(";");
            values.Add(new ProtoEnumValue(token, number));
        }
        var result = new ProtoEnum(Qualify(scope, name), name, values);
        enums[result.FullName] = result;
        return result;
    }

    private static ProtoMessage Resolve(RawMessage message, HashSet<string> known) {
        var fields = message.Fields.Select(f => ResolveField(f, message.FullName, known)).ToList();
        return new ProtoMessage(message.FullName, message.Name, fields, message.Nested, message.Oneofs);
    }

    private static ProtoField ResolveField(RawField field, string scope, HashSet<string> known) {
        if (field.Type.StartsWith("map<", StringComparison.Ordinal)) {
            var parts = field.Type[4..^1].Split(',');
            var value = ResolveType(parts[1], scope, known);
            return new ProtoField(field.Name, field.Number, field.Repeated, $"map<{parts[0]},{value}>", Scalars.Contains(parts[1]) ? null : value, field.Oneof);
        }
        if (Scalars.Contains(field.Type))
            return new ProtoField(field.Name, field.Number, field.Repeated, field.Type, null, field.Oneof);
        var resolved = ResolveType(field.Type, scope, known);
        return new ProtoField(field.Name, field.Number, field.Repeated, resolved, resolved, field.Oneof);
    }

    private static string ResolveType(string type, string scope, HashSet<string> known) {
        if (Scalars.Contains(type))
            return type;
        if (type.StartsWith('.'))
            return type[1..];
        for (var current = scope; current.Length > 0; current = Parent(current)) {
            var candidate = $"{current}.{type}";
            if (known.Contains(candidate))
                return candidate;
        }
        return type;
    }

    private static string Parent(string name) {
        var dot = name.LastIndexOf('.');
        return dot < 0 ? "" : name[..dot];
    }

    private sealed record RawField(string Name, int Number, bool Repeated, string Type, string? Oneof);

    private sealed class RawMessage(string fullName, string name) {
        public string FullName { get; } = fullName;
        public string Name { get; } = name;
        public List<RawField> Fields { get; } = [];
        public List<string> Nested { get; } = [];
        public List<string> Oneofs { get; } = [];
    }
}

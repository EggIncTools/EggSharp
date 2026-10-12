namespace EggSharp.ProtoParity;

public enum ViolationKind { Removed, Renamed, Renumbered, TypeChanged, CSharpNameChanged }

public sealed record Violation(ViolationKind Kind, string Subject, string Detail) {
    public override string ToString() => $"{Kind}: {Subject}: {Detail}";
}

public static class ParityChecker {
    public static IReadOnlyList<Violation> Check(ProtoSchema baseline, ProtoSchema head, IEnumerable<string> roots) {
        var violations = new List<Violation>();
        foreach (var name in Reachable(baseline, roots)) {
            if (baseline.Messages.TryGetValue(name, out var message))
                CheckMessage(message, head, violations);
            else if (baseline.Enums.TryGetValue(name, out var protoEnum))
                CheckEnum(protoEnum, head, violations);
        }
        return violations;
    }

    private static List<string> Reachable(ProtoSchema schema, IEnumerable<string> roots) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var order = new List<string>();
        var pending = new Stack<string>(roots.Where(schema.Contains).Reverse());
        while (pending.TryPop(out var name)) {
            if (!seen.Add(name))
                continue;
            order.Add(name);
            if (!schema.Messages.TryGetValue(name, out var message))
                continue;
            foreach (var next in message.Fields.Select(f => f.Reference).OfType<string>().Concat(message.Nested).Where(schema.Contains).Reverse())
                pending.Push(next);
        }
        return order;
    }

    private static void CheckMessage(ProtoMessage before, ProtoSchema head, List<Violation> violations) {
        if (!head.Messages.TryGetValue(before.FullName, out var after)) {
            violations.Add(new Violation(ViolationKind.Removed, before.FullName, head.Enums.ContainsKey(before.FullName) ? "message became an enum" : "message removed or renamed"));
            return;
        }
        foreach (var oneof in before.Oneofs.Where(o => !after.Oneofs.Contains(o)))
            violations.Add(new Violation(ViolationKind.Removed, $"{before.FullName}.{oneof}", "oneof removed or renamed"));
        foreach (var field in before.Fields) {
            var subject = $"{before.FullName}.{field.Name}";
            var match = after.Fields.FirstOrDefault(f => f.Name == field.Name) ?? after.Fields.FirstOrDefault(f => f.Number == field.Number);
            if (match is null) {
                violations.Add(new Violation(ViolationKind.Removed, subject, $"field {field.Number} removed"));
                continue;
            }
            if (match.Name != field.Name)
                violations.Add(new Violation(ViolationKind.Renamed, subject, $"field {field.Number} renamed to {match.Name}"));
            if (match.Number != field.Number)
                violations.Add(new Violation(ViolationKind.Renumbered, subject, $"field number {field.Number} became {match.Number}"));
            if (Describe(match) != Describe(field))
                violations.Add(new Violation(ViolationKind.TypeChanged, subject, $"{Describe(field)} became {Describe(match)}"));
            var beforeName = CSharpNames.Property(field.Name, before.Name);
            var afterName = CSharpNames.Property(match.Name, after.Name);
            if (beforeName != afterName)
                violations.Add(new Violation(ViolationKind.CSharpNameChanged, subject, $"property {beforeName} became {afterName}"));
        }
    }

    private static void CheckEnum(ProtoEnum before, ProtoSchema head, List<Violation> violations) {
        if (!head.Enums.TryGetValue(before.FullName, out var after)) {
            violations.Add(new Violation(ViolationKind.Removed, before.FullName, head.Messages.ContainsKey(before.FullName) ? "enum became a message" : "enum removed or renamed"));
            return;
        }
        foreach (var value in before.Values) {
            var subject = $"{before.FullName}.{value.Name}";
            var match = after.Values.FirstOrDefault(v => v.Name == value.Name) ?? after.Values.FirstOrDefault(v => v.Number == value.Number);
            if (match is null) {
                violations.Add(new Violation(ViolationKind.Removed, subject, $"enum value {value.Number} removed"));
                continue;
            }
            if (match.Name != value.Name)
                violations.Add(new Violation(ViolationKind.Renamed, subject, $"enum value {value.Number} renamed to {match.Name}"));
            if (match.Number != value.Number)
                violations.Add(new Violation(ViolationKind.Renumbered, subject, $"enum value number {value.Number} became {match.Number}"));
            var beforeName = CSharpNames.EnumValue(before.Name, value.Name);
            var afterName = CSharpNames.EnumValue(after.Name, match.Name);
            if (beforeName != afterName)
                violations.Add(new Violation(ViolationKind.CSharpNameChanged, subject, $"enum member {beforeName} became {afterName}"));
        }
    }

    private static string Describe(ProtoField field) {
        var label = field.Repeated ? "repeated " : "";
        var oneof = field.Oneof is null ? "" : $" in oneof {field.Oneof}";
        return $"{label}{field.Type}{oneof}";
    }
}

namespace EggSharp.ProtoParity;

public sealed record ProtoField(string Name, int Number, bool Repeated, string Type, string? Reference, string? Oneof);

public sealed record ProtoMessage(string FullName, string Name, IReadOnlyList<ProtoField> Fields, IReadOnlyList<string> Nested, IReadOnlyList<string> Oneofs);

public sealed record ProtoEnumValue(string Name, int Number);

public sealed record ProtoEnum(string FullName, string Name, IReadOnlyList<ProtoEnumValue> Values);

public sealed record ProtoSchema(string Package, IReadOnlyDictionary<string, ProtoMessage> Messages, IReadOnlyDictionary<string, ProtoEnum> Enums) {
    public bool Contains(string fullName) => Messages.ContainsKey(fullName) || Enums.ContainsKey(fullName);
}

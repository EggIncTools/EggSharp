using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using EggSharp.Core;
using Ei;
using ProtoBuf;

namespace EggSharp.Api;

internal sealed class ProtobufNetEiCodec : IEiCodec {
    private const string Package = "ei";
    private const string RinfoField = "rinfo";
    private const string RinfoProperty = "Rinfo";

    private static readonly ConcurrentDictionary<Type, string> Names = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> RinfoProperties = new();

    public byte[] Serialize(object message) {
        ArgumentNullException.ThrowIfNull(message);
        using var stream = new MemoryStream();
        Serializer.NonGeneric.Serialize(stream, message);
        return stream.ToArray();
    }

    public object Parse(Type messageType, byte[] bytes) {
        try {
            return ParseExact(messageType, bytes);
        } catch (Exception e) when (ProtobufUtf8Sanitizer.IsInvalidUtf8(e)) {
            return ParseExact(messageType, ProtobufUtf8Sanitizer.Sanitize(bytes));
        }
    }

    public byte[] WrapAuthenticated(byte[] message, string? code) {
        var envelope = new AuthenticatedMessage { Message = message };
        if (code is not null)
            envelope.Code = code;
        return Serialize(envelope);
    }

    public bool TryUnwrapAuthenticated(byte[] bytes, out byte[] message, out bool compressed) {
        message = [];
        compressed = false;
        AuthenticatedMessage envelope;
        try {
            envelope = (AuthenticatedMessage)Parse(typeof(AuthenticatedMessage), bytes);
        } catch (ProtoException) {
            return false;
        } catch (EndOfStreamException) {
            return false;
        }
        message = envelope.Message ?? [];
        compressed = envelope.Compressed;
        return true;
    }

    public void SetRinfo(object request, RinfoValues rinfo) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rinfo);
        if (request is BasicRequestInfo direct) {
            ProtobufNetEiRinfo.Apply(direct, rinfo);
            return;
        }
        if (request is EggIncFirstContactRequest first)
            ProtobufNetEiRinfo.ApplyFirstContact(first, rinfo, first.ShouldSerializePlatform());
        var type = request.GetType();
        var property = RinfoProperties.GetOrAdd(type, FindRinfoProperty)
            ?? throw new ArgumentException($"{MessageName(type)} has no rinfo field.", nameof(request));
        var info = property.GetValue(request) as BasicRequestInfo ?? new BasicRequestInfo();
        ProtobufNetEiRinfo.Apply(info, rinfo);
        property.SetValue(request, info);
    }

    public string MessageName(Type messageType) => Names.GetOrAdd(messageType, BuildName);

    private static object ParseExact(Type messageType, byte[] bytes) {
        using var stream = new MemoryStream(bytes, false);
        return Serializer.NonGeneric.Deserialize(messageType, stream);
    }

    private static PropertyInfo? FindRinfoProperty(Type type) {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(p => p.PropertyType == typeof(BasicRequestInfo) && IsRinfo(p));
    }

    private static bool IsRinfo(PropertyInfo property) {
        return property.GetCustomAttribute<ProtoMemberAttribute>()?.Name == RinfoField || property.Name == RinfoProperty;
    }

    private static string BuildName(Type messageType) {
        var contract = messageType.GetCustomAttribute<ProtoContractAttribute>()
            ?? throw new ArgumentException($"{messageType.FullName} is not a generated protobuf message.", nameof(messageType));
        var name = string.IsNullOrEmpty(contract.Name) ? messageType.Name : contract.Name;
        return messageType.DeclaringType is { } outer ? $"{BuildName(outer)}.{name}" : $"{Package}.{name}";
    }
}

using System;
using System.Collections.Concurrent;
using System.Reflection;
using EggSharp.Core;
using Ei;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace EggSharp.Api;

internal sealed class GoogleEiCodec : IEiCodec {
    private static readonly ConcurrentDictionary<Type, MessageDescriptor> Descriptors = new();

    public byte[] Serialize(object message) => ((IMessage)message).ToByteArray();

    public object Parse(Type messageType, byte[] bytes) {
        try {
            return ParseExact(messageType, bytes);
        } catch (Exception e) when (ProtobufUtf8Sanitizer.IsInvalidUtf8(e)) {
            return ParseExact(messageType, ProtobufUtf8Sanitizer.Sanitize(bytes));
        }
    }

    public byte[] WrapAuthenticated(byte[] message, string? code) {
        var envelope = new AuthenticatedMessage { Message = ByteString.CopyFrom(message) };
        if (code is not null)
            envelope.Code = code;
        return envelope.ToByteArray();
    }

    public bool TryUnwrapAuthenticated(byte[] bytes, out byte[] message, out bool compressed) {
        message = [];
        compressed = false;
        AuthenticatedMessage envelope;
        try {
            envelope = (AuthenticatedMessage)Parse(typeof(AuthenticatedMessage), bytes);
        } catch (InvalidProtocolBufferException) {
            return false;
        }
        message = envelope.Message.ToByteArray();
        compressed = envelope.Compressed;
        return true;
    }

    public void SetRinfo(object request, RinfoValues rinfo) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(rinfo);
        if (request is BasicRequestInfo direct) {
            EiRinfo.Apply(direct, rinfo);
            return;
        }
        if (request is EggIncFirstContactRequest first)
            EiRinfo.ApplyFirstContact(first, rinfo, first.HasPlatform);
        var message = (IMessage)request;
        var field = message.Descriptor.FindFieldByName("rinfo")
            ?? throw new ArgumentException($"{message.Descriptor.FullName} has no rinfo field.", nameof(request));
        var info = field.Accessor.GetValue(message) as BasicRequestInfo ?? new BasicRequestInfo();
        EiRinfo.Apply(info, rinfo);
        field.Accessor.SetValue(message, info);
    }

    public string MessageName(Type messageType) => DescriptorOf(messageType).FullName;

    private static IMessage ParseExact(Type messageType, byte[] bytes) => DescriptorOf(messageType).Parser.ParseFrom(bytes);

    private static MessageDescriptor DescriptorOf(Type messageType) {
        if (Descriptors.TryGetValue(messageType, out var cached))
            return cached;
        var property = messageType.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static);
        var descriptor = property?.GetValue(null) as MessageDescriptor
            ?? throw new ArgumentException($"{messageType.FullName} is not a generated protobuf message.", nameof(messageType));
        return Descriptors.GetOrAdd(messageType, descriptor);
    }
}

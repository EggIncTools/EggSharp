namespace EggSharp.Api;

public interface IEiCodec {
    byte[] Serialize(object message);

    object Parse(Type messageType, byte[] bytes);

    byte[] WrapAuthenticated(byte[] message, string? code);

    bool TryUnwrapAuthenticated(byte[] bytes, out byte[] message, out bool compressed);

    void SetRinfo(object request, RinfoValues rinfo);

    string MessageName(Type messageType);
}

public sealed record RinfoValues(string? UserId, uint ClientVersion, string Version, string Build, string Platform, string? Country, string? Language, bool? Debug, string? DeviceId);

#nullable enable
using System;
using Ei;

namespace EggSharp.Api;

internal static class ProtobufNetEiRinfo {
    public static void Apply(BasicRequestInfo info, RinfoValues rinfo) {
        if (rinfo.UserId is not null) {
            info.EiUserId = rinfo.UserId;
        }
        info.ClientVersion = rinfo.ClientVersion;
        info.Version = rinfo.Version;
        info.Build = rinfo.Build;
        info.Platform = rinfo.Platform;
        if (rinfo.Country is not null) {
            info.Country = rinfo.Country;
        }
        if (rinfo.Language is not null) {
            info.Language = rinfo.Language;
        }
        if (rinfo.Debug is { } debug) {
            info.Debug = debug;
        }
    }

    public static void ApplyFirstContact(EggIncFirstContactRequest request, RinfoValues rinfo, bool platformSet) {
        if (rinfo.DeviceId is not null) {
            request.DeviceId = rinfo.DeviceId;
        }
        if (!platformSet && TryPlatform(rinfo.Platform, out var platform)) {
            request.Platform = platform;
        }
    }

    private static bool TryPlatform(string name, out Platform platform) {
        return Enum.TryParse(name, true, out platform)
            || Enum.TryParse(name.Replace("_", "", StringComparison.Ordinal), true, out platform);
    }
}

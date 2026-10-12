using System.Linq;
using EggSharp.Api;
using Ei;
using Google.Protobuf;

namespace EggSharp.Ei.Google.Tests;

public sealed class GoogleEiCodecTests {
    private readonly GoogleEiCodec _codec = new();

    [Fact]
    public void AuthenticatedMessage_RoundTrips() {
        var inner = new MissionRequest { EiUserId = "EI1234567890123456", ClientVersion = 70 }.ToByteArray();
        var wrapped = _codec.WrapAuthenticated(inner, "signature");

        var envelope = (AuthenticatedMessage)_codec.Parse(typeof(AuthenticatedMessage), wrapped);
        Assert.Equal("signature", envelope.Code);
        Assert.True(_codec.TryUnwrapAuthenticated(wrapped, out var message, out var compressed));
        Assert.Equal(inner, message);
        Assert.False(compressed);
    }

    [Fact]
    public void Backup_UnknownField_SurvivesParseAndSerialize() {
        byte[] unknown = [0xC0, 0xB2, 0x04, 0x2A];
        var input = new Backup { UserId = "user" }.ToByteArray().Concat(unknown).ToArray();

        var output = _codec.Serialize(_codec.Parse(typeof(Backup), input));

        Assert.Equal(input, output);
    }

    [Fact]
    public void Parse_InvalidUtf8_RetriesOnSanitizedBytes() {
        byte[] input = [0x12, 0x02, 0x61, 0xFF];

        var backup = (Backup)_codec.Parse(typeof(Backup), input);

        Assert.Equal("a?", backup.UserName);
    }

    [Fact]
    public void SetRinfo_BasicRequestInfo_FillsFieldsAndSkipsNulls() {
        var info = new BasicRequestInfo();

        _codec.SetRinfo(info, new RinfoValues(null, 70, "1.35", "111300", "IOS", null, "en", null, null));

        Assert.False(info.HasEiUserId);
        Assert.Equal(70u, info.ClientVersion);
        Assert.Equal("1.35", info.Version);
        Assert.Equal("111300", info.Build);
        Assert.Equal("IOS", info.Platform);
        Assert.False(info.HasCountry);
        Assert.Equal("en", info.Language);
        Assert.False(info.HasDebug);
    }

    [Fact]
    public void SetRinfo_WrappedRequest_SetsRinfoField() {
        var request = new ContractCoopStatusRequest { CoopIdentifier = "coop" };

        _codec.SetRinfo(request, new RinfoValues("EI1234567890123456", 70, "1.35", "111300", "IOS", "US", null, false, null));

        Assert.Equal("EI1234567890123456", request.Rinfo.EiUserId);
        Assert.Equal("US", request.Rinfo.Country);
        Assert.False(request.Rinfo.HasLanguage);
        Assert.True(request.Rinfo.HasDebug);
        Assert.False(request.Rinfo.Debug);
        Assert.Equal("coop", request.CoopIdentifier);
    }
}

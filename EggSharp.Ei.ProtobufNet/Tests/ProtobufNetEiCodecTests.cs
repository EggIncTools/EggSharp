using System.Linq;
using EggSharp.Api;
using Ei;

namespace EggSharp.Ei.ProtobufNet.Tests;

public sealed class ProtobufNetEiCodecTests {
    private readonly ProtobufNetEiCodec _codec = new();

    [Fact]
    public void AuthenticatedMessage_RoundTrips() {
        var inner = _codec.Serialize(new MissionRequest { EiUserId = "EI1234567890123456", ClientVersion = 70 });
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
        var input = _codec.Serialize(new Backup { UserId = "user" }).Concat(unknown).ToArray();

        var output = _codec.Serialize(_codec.Parse(typeof(Backup), input));

        Assert.Equal(input, output);
    }

    [Fact]
    public void SetRinfo_BasicRequestInfo_FillsFieldsAndSkipsNulls() {
        var info = new BasicRequestInfo();

        _codec.SetRinfo(info, new RinfoValues(null, 70, "1.35", "111300", "IOS", null, "en", null, null));

        Assert.False(info.ShouldSerializeEiUserId());
        Assert.Equal(70u, info.ClientVersion);
        Assert.Equal("1.35", info.Version);
        Assert.Equal("111300", info.Build);
        Assert.Equal("IOS", info.Platform);
        Assert.False(info.ShouldSerializeCountry());
        Assert.Equal("en", info.Language);
        Assert.False(info.ShouldSerializeDebug());
    }

    [Fact]
    public void SetRinfo_WrappedRequest_SetsRinfoField() {
        var request = new ContractCoopStatusRequest { CoopIdentifier = "coop" };

        _codec.SetRinfo(request, new RinfoValues("EI1234567890123456", 70, "1.35", "111300", "IOS", "US", null, false, null));

        Assert.Equal("EI1234567890123456", request.Rinfo.EiUserId);
        Assert.Equal("US", request.Rinfo.Country);
        Assert.False(request.Rinfo.ShouldSerializeLanguage());
        Assert.True(request.Rinfo.ShouldSerializeDebug());
        Assert.False(request.Rinfo.Debug);
        Assert.Equal("coop", request.CoopIdentifier);
    }
}

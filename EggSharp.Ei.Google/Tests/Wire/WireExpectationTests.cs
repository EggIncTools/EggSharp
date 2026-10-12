using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EggSharp.Api;
using EggSharp.Core;
using Ei;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Time.Testing;

namespace EggSharp.Ei.Google.Tests.Wire;

public sealed class WireExpectationTests {
    private const string Origin = "https://www.auxbrain.com/";
    private const string FormContentType = "application/x-www-form-urlencoded";
    private const string User = "EI1111222233334444";
    private const string Bot = "EI5555666677778888";
    private const string Reference = "EI9999000011112222";
    private const string NonEiUser = "G:1234567890";
    private const string ContractId = "wire-contract";
    private const string CoopId = "wire-coop";

    private static readonly VersionTriple Live = new(75, "1.35.6", "1.35.6.3");
    private static readonly VersionTriple Updated = new(76, "1.36.0", "1.36.0.1");
    private static readonly AuxbrainSigner Signer = new(WireHarness.SaltPhrase);
    private static readonly ApiCallOptions UserOptions = new(UserId: User);

    private static readonly Dictionary<string, (Version Http, string[] Headers)> Profiles = new() {
        [ApiPresets.AndroidProfile] = (HttpVersion.Version11, Headers(
            ("User-Agent", "Dalvik/2.1.0 (Linux; U; Android 9; SM-G960U1 Build/PPR1.180610.011)"),
            ("Accept-Encoding", "gzip"),
            ("Connection", "Keep-Alive"))),
        [ApiPresets.IosProfile] = (HttpVersion.Version11, Headers(
            ("User-Agent", "egginc/1.26.1.3 CFNetwork/1335.0.3 Darwin/21.6.0"),
            ("Accept-Encoding", "gzip, deflate, br"),
            ("Connection", "Keep-Alive"))),
        [ApiPresets.CoopStatusProfile] = (HttpVersion.Version20, Headers(
            ("cookie", "session=9cd692e4-050e-4cb9-a305-993bd28441b2"),
            ("user-agent", "egginc/1.35.3.1 CFNetwork/1410.1 Darwin/22.6.0"),
            ("accept-encoding", "gzip, deflate, br"),
            ("accept-language", "en-US,en;q=0.9"),
            ("accept", "*/*")))
    };

    private static readonly Dictionary<string, TableCase> Cases = new() {
        ["ei/join_coop"] = new(ApiPresets.IosProfile, ExpectedRinfo.WithUser, false,
            r => new JoinCoopRequest { Rinfo = r, ContractIdentifier = ContractId, CoopIdentifier = CoopId, UserId = User, UserName = "wire", ClientVersion = Live.ClientVersion },
            (api, m) => api.Post<JoinCoopRequest, JoinCoopResponse>((JoinCoopRequest)m, UserOptions)),
        ["ei/get_periodicals"] = new(ApiPresets.IosProfile, ExpectedRinfo.WithoutUser, false,
            r => Periodicals(Reference, Live.ClientVersion, r),
            (api, m) => api.Post<GetPeriodicalsRequest, PeriodicalsResponse>((GetPeriodicalsRequest)m, UserOptions)),
        ["ei_ctx/get_contracts_info"] = new(ApiPresets.IosProfile, ExpectedRinfo.WithUser, true,
            ContractsInfo,
            (api, m) => api.Post<ContractsInfoRequest, ContractsInfoResponse>((ContractsInfoRequest)m, UserOptions)),
        ["ei/create_coop"] = new(ApiPresets.IosProfile, ExpectedRinfo.WithUser, false,
            r => new CreateCoopRequest { Rinfo = r, ContractIdentifier = ContractId, CoopIdentifier = CoopId, UserId = User, UserName = "wire", ClientVersion = Live.ClientVersion },
            (api, m) => api.Post<CreateCoopRequest, CreateCoopResponse>((CreateCoopRequest)m, UserOptions)),
        ["ei/update_coop_permissions"] = new(ApiPresets.IosProfile, ExpectedRinfo.WithUser, false,
            r => new UpdateCoopPermissionsRequest { Rinfo = r, ContractIdentifier = ContractId, CoopIdentifier = CoopId, RequestingUserId = User, Public = true, ClientVersion = Live.ClientVersion },
            (api, m) => api.Post<UpdateCoopPermissionsRequest, UpdateCoopPermissionsResponse>((UpdateCoopPermissionsRequest)m, UserOptions)),
        ["ei/update_coop_status_secure"] = new(ApiPresets.IosProfile, ExpectedRinfo.WithUser, true,
            r => new ContractCoopStatusUpdateRequest { Rinfo = r, UserId = User, ContractIdentifier = ContractId, CoopIdentifier = CoopId, Amount = 1.5e15, Rate = 2.5e12 },
            (api, m) => api.Post<ContractCoopStatusUpdateRequest, ContractCoopStatusUpdateResponse>((ContractCoopStatusUpdateRequest)m, UserOptions)),
        ["ei/get_config"] = new(ApiPresets.IosProfile, ExpectedRinfo.WithUser, false,
            r => new ConfigRequest { Rinfo = r, SoulEggs = 1e21, ContractsUnlocked = true, ArtifactsUnlocked = true },
            (api, m) => api.Post<ConfigRequest, ConfigResponse>((ConfigRequest)m, UserOptions)),
        ["ei/kick_player_coop"] = new(ApiPresets.AndroidProfile, ExpectedRinfo.None, false,
            _ => new KickPlayerCoopRequest { ContractIdentifier = ContractId, CoopIdentifier = CoopId, PlayerIdentifier = Bot, RequestingUserId = User, Reason = KickPlayerCoopRequest.Types.Reason.Idle, ClientVersion = Live.ClientVersion },
            (api, m) => api.Send((KickPlayerCoopRequest)m, UserOptions)),
        ["ei_ctx/get_contract_player_info"] = new(ApiPresets.IosProfile, ExpectedRinfo.WithUser, true,
            r => r ?? new BasicRequestInfo(),
            (api, m) => api.Post<BasicRequestInfo, ContractPlayerInfo>((BasicRequestInfo)m, UserOptions)),
        ["ei_ctx/get_contracts_archive"] = new(ApiPresets.IosProfile, ExpectedRinfo.WithUser, false,
            r => r ?? new BasicRequestInfo(),
            (api, m) => api.Post<BasicRequestInfo, MyContracts>((BasicRequestInfo)m, UserOptions)),
        ["ei_afx/complete_mission"] = new(ApiPresets.IosProfile, ExpectedRinfo.None, false,
            _ => Mission(),
            (api, m) => api.Post<MissionRequest, CompleteMissionResponse>((MissionRequest)m, UserOptions))
    };

    public static TheoryData<string> Paths {
        get {
            var data = new TheoryData<string>();
            foreach (var path in Cases.Keys)
                data.Add(path);
            return data;
        }
    }

    [Fact]
    public void Egg9000_EveryTableEntry_HasAnExpectation() =>
        Assert.Equal(
            ApiPresets.Egg9000.Endpoints.Values.Select(e => e.Path).Order(StringComparer.Ordinal),
            Cases.Keys.Order(StringComparer.Ordinal));

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Egg9000_TableEntry_MatchesApiShape(string path) {
        var entry = Cases[path];
        using var harness = WireHarness.Create(Live);

        await entry.Send(harness.Api, entry.Build(null));

        var rinfo = entry.Rinfo switch {
            ExpectedRinfo.WithUser => Info(Live, User),
            ExpectedRinfo.WithoutUser => Info(Live, null),
            _ => null
        };
        var inner = entry.Build(rinfo).ToByteArray();
        AssertRequest(harness.Single, path, entry.Profile, await FormBody(entry.Signed ? Wrap(inner) : inner));
    }

    [Fact]
    public async Task Egg9000_BotFirstContact_MatchesApiShape() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.BotFirstContact(User);

        AssertRequest(harness.Single, "ei/bot_first_contact", ApiPresets.AndroidProfile, await FormBody(FirstContactExpected(User).ToByteArray()));
        var sent = EggIncFirstContactRequest.Parser.ParseFrom(Payload(harness.Single));
        Assert.Equal(sent.EiUserId, sent.DeviceId);
        Assert.True(sent.HasUsername);
        Assert.Equal(string.Empty, sent.Username);
        Assert.True(sent.Rinfo.HasDebug);
        Assert.False(sent.Rinfo.Debug);
    }

    [Fact]
    public async Task Egg9000_FirstContact_EiId_FallsBackToSignedSecure() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.FirstContact(User);

        Assert.Equal(2, harness.Recorder.Requests.Count);
        AssertRequest(harness.Recorder.Requests[0], "ei/bot_first_contact", ApiPresets.AndroidProfile, await FormBody(FirstContactExpected(User).ToByteArray()));
        AssertRequest(harness.Recorder.Requests[1], "ei/first_contact_secure", ApiPresets.AndroidProfile, await FormBody(Wrap(FirstContactExpected(User).ToByteArray())));
    }

    [Fact]
    public async Task Egg9000_FirstContact_NonEiId_SendsSignedUserId() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.FirstContact(NonEiUser);

        var expected = new EggIncFirstContactRequest { ClientVersion = Live.ClientVersion, Platform = Platform.Droid, UserId = NonEiUser };
        Assert.Equal(2, harness.Recorder.Requests.Count);
        AssertRequest(harness.Recorder.Requests[1], "ei/first_contact_secure", ApiPresets.AndroidProfile, await FormBody(Wrap(expected.ToByteArray())));
    }

    [Fact]
    public async Task Egg9000_CompleteMission_MatchesApiShape() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.CompleteMission(Mission(), UserOptions);

        AssertRequest(harness.Single, "ei_afx/complete_mission", ApiPresets.IosProfile, await FormBody(Mission().ToByteArray()));
    }

    [Fact]
    public async Task Egg9000_GetCoopStatus_UsesLiveTripleAndClockSeconds() {
        using var harness = WireHarness.Create(Live);
        harness.Versions.Set(Updated);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 8, 30, 15, 250, TimeSpan.Zero));

        await harness.Api.GetCoopStatus(ContractId, "Wire-COOP", User, clock);

        AssertRequest(harness.Single, "ei/coop_status", ApiPresets.CoopStatusProfile, await FormBody(CoopStatusExpected(User, clock).ToByteArray()));
    }

    [Fact]
    public async Task Egg9000_GetCoopStatusBot_UsesBotIdOnAndroid() {
        using var harness = WireHarness.Create(Live);
        harness.Versions.Set(Updated);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 8, 30, 15, 250, TimeSpan.Zero));

        await harness.Api.GetCoopStatusBot(ContractId, "Wire-COOP", Bot, clock);

        AssertRequest(harness.Single, "ei/coop_status_bot", ApiPresets.AndroidProfile, await FormBody(CoopStatusExpected(Bot, clock).ToByteArray()));
    }

    [Fact]
    public async Task Egg9000_GetPeriodicals_CarriesConstantsAndNoRinfoUser() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.GetPeriodicals(Periodicals(Reference, Live.ClientVersion), UserOptions);

        AssertRequest(harness.Single, "ei/get_periodicals", ApiPresets.IosProfile, await FormBody(Periodicals(Reference, Live.ClientVersion, Info(Live, null)).ToByteArray()));
    }

    [Fact]
    public async Task Egg9000_ValidateVersions_SendsCandidateWithoutChangingLive() {
        using var harness = WireHarness.Create(Live);
        var candidate = new VersionTriple(77, "1.36.1", "1.36.1.2");

        var valid = await harness.Api.ValidateVersions(candidate, Periodicals(Reference, candidate.ClientVersion));

        Assert.False(valid);
        Assert.Equal(Live, harness.Api.Versions);
        AssertRequest(harness.Single, "ei/get_periodicals", ApiPresets.IosProfile, await FormBody(Periodicals(Reference, candidate.ClientVersion, Info(candidate, null)).ToByteArray()));
    }

    [Fact]
    public async Task Egg9000_GetContractsInfo_MatchesApiShape() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.GetContractsInfo(ContractsInfo(null), UserOptions);

        AssertRequest(harness.Single, "ei_ctx/get_contracts_info", ApiPresets.IosProfile, await FormBody(Wrap(ContractsInfo(Info(Live, User)).ToByteArray())));
    }

    [Fact]
    public async Task Egg9000_GetContractPlayerInfo_SignedOnAndroid() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.GetContractPlayerInfo(UserOptions);

        AssertRequest(harness.Single, "ei_ctx/get_contract_player_info", ApiPresets.AndroidProfile, await FormBody(Wrap(Info(Live, User).ToByteArray())));
    }

    [Fact]
    public async Task Egg9000_GetContractsArchive_UnsignedBareRinfoOnAndroid() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.GetContractsArchive(UserOptions);

        AssertRequest(harness.Single, "ei_ctx/get_contracts_archive", ApiPresets.AndroidProfile, await FormBody(Info(Live, User).ToByteArray()));
    }

    [Fact]
    public async Task Egg9000_GetSeasonInfos_SignedOnAndroid() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.GetSeasonInfos(UserOptions);

        AssertRequest(harness.Single, "ei_ctx/get_season_infos_v2", ApiPresets.AndroidProfile, await FormBody(Wrap(Info(Live, User).ToByteArray())));
    }

    [Fact]
    public async Task Egg9000_GetUserSubscription_EmptyBodyWithPathParam() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.GetUserSubscription(User);

        AssertRequest(harness.Single, "ei_srv/subscription_status/" + User, ApiPresets.IosProfile, []);
    }

    [Fact]
    public async Task Egg9000_SignedPayload_HashesSaltMutatedCopyAndCarriesOriginal() {
        using var harness = WireHarness.Create(Live);

        await harness.Api.GetContractsInfo(ContractsInfo(null), UserOptions);

        var envelope = AuthenticatedMessage.Parser.ParseFrom(Payload(harness.Single));
        var inner = ContractsInfo(Info(Live, User)).ToByteArray();
        var index = (int)(0x3b9af419u % (uint)inner.Length);
        Assert.NotEqual((byte)0x1b, inner[index]);
        var mutated = inner.ToArray();
        mutated[index] = 0x1b;
        var salt = Encoding.ASCII.GetBytes(Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(WireHarness.SaltPhrase))));
        Assert.Equal(inner, envelope.Message.ToByteArray());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData([.. mutated, .. salt])), envelope.Code);
    }

    [Fact]
    public async Task Egg9000_FirstContact_SecureLegException_IsBotException() {
        using var harness = WireHarness.Create(Live);
        harness.Recorder.Respond = r => r.RequestUri!.AbsolutePath.EndsWith("first_contact_secure", StringComparison.Ordinal)
            ? throw new InvalidOperationException("secure down")
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };

        var outcome = await harness.Api.FirstContact(User);

        Assert.False(outcome.Success);
        Assert.Equal("Bot Exception: secure down", outcome.Error);
    }

    [Fact]
    public async Task Egg9000_FirstContact_NoSalt_ReturnsBotErrorWithoutSecureCall() {
        using var harness = WireHarness.Create(Live, null);

        var outcome = await harness.Api.FirstContact(User);

        Assert.Equal("ei/bot_first_contact", harness.Single.Uri.AbsolutePath.TrimStart('/'));
        Assert.False(outcome.Success);
        Assert.Equal("bot_first_contact returned no backup", outcome.Error);
    }

    [Fact]
    public async Task Egg9000_FirstContact_SecureResponse_SucceedsRegardlessOfErrorCodeOrBackup() {
        using var harness = WireHarness.Create(Live);
        var secure = new EggIncFirstContactResponse { ErrorCode = 3 };
        harness.Recorder.Respond = r => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(r.RequestUri!.AbsolutePath.EndsWith("first_contact_secure", StringComparison.Ordinal)
                ? Convert.ToBase64String(new AuthenticatedMessage { Message = secure.ToByteString() }.ToByteArray())
                : "")
        };

        var outcome = await harness.Api.FirstContact(User);

        Assert.True(outcome.Success);
        Assert.Equal(3u, outcome.Response!.ErrorCode);
    }

    [Fact]
    public async Task Egg9000_EmptyOkBody_DecodesToEmptyMessage() {
        using var harness = WireHarness.Create(Live);

        var outcome = await harness.Api.GetUserSubscription(User);

        Assert.True(outcome.Success);
        Assert.Equal(new UserSubscriptionInfo(), outcome.Response);
    }

    [Fact]
    public async Task Egg9000_AuthenticatedEnvelopeWithoutMessage_DecodesToEmptyMessage() {
        using var harness = WireHarness.Create(Live);
        harness.Recorder.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Convert.ToBase64String(new AuthenticatedMessage { Code = "x" }.ToByteArray())) };

        var outcome = await harness.Api.GetUserSubscription(User);

        Assert.True(outcome.Success);
        Assert.Equal(new UserSubscriptionInfo(), outcome.Response);
    }

    [Fact]
    public async Task Egg9000_CoopStatus_EmptyOkBody_FailsThroughIdMismatchWithoutThrowing() {
        using var harness = WireHarness.Create(Live);

        var outcome = await harness.Api.GetCoopStatus(ContractId, CoopId, User);

        Assert.False(outcome.Success);
        Assert.Equal("No response", outcome.Error);
    }

    private static void AssertRequest(WireRequest actual, string path, string profile, byte[] body) {
        var expected = Profiles[profile];
        Assert.Equal(new Uri(Origin + path), actual.Uri);
        Assert.Equal(expected.Http, actual.Version);
        Assert.Equal(expected.Headers, actual.Headers);
        Assert.Equal(body.Length == 0 ? null : FormContentType, actual.ContentType);
        Assert.Equal(body, actual.Body);
    }

    private static string[] Headers(params (string Name, string Value)[] headers) {
        using var probe = new HttpRequestMessage();
        foreach (var (name, value) in headers)
            probe.Headers.Add(name, value);
        return WireRecorder.Normalize(probe.Headers);
    }

    private static BasicRequestInfo Info(VersionTriple versions, string? userId) {
        var info = new BasicRequestInfo {
            ClientVersion = versions.ClientVersion,
            Version = versions.AppVersion,
            Build = versions.AppBuild,
            Platform = "IOS",
            Country = "US",
            Language = "en",
            Debug = false
        };
        if (userId is not null)
            info.EiUserId = userId;
        return info;
    }

    private static GetPeriodicalsRequest Periodicals(string userId, uint clientVersion, BasicRequestInfo? rinfo = null) => new() {
        Rinfo = rinfo,
        UserId = userId,
        PiggyFull = false,
        PiggyFoundFull = false,
        SecondsFullRealtime = 2339576.17448521,
        SecondsFullGametime = 391564.659540082,
        SoulEggs = 570149167.28294,
        CurrentClientVersion = clientVersion,
        Debug = false
    };

    private static ContractsInfoRequest ContractsInfo(BasicRequestInfo? rinfo) => new() {
        Rinfo = rinfo,
        ClientVersion = Live.ClientVersion,
        ContractIdentifiers = { ContractId }
    };

    private static MissionRequest Mission() => new() {
        EiUserId = User,
        ClientVersion = Live.ClientVersion,
        Info = new MissionInfo { Identifier = "wire-mission" }
    };

    private static EggIncFirstContactRequest FirstContactExpected(string userId) => new() {
        ClientVersion = Live.ClientVersion,
        Platform = Platform.Droid,
        EiUserId = userId,
        DeviceId = userId,
        Username = "",
        Rinfo = Info(Live, userId)
    };

    private static ContractCoopStatusRequest CoopStatusExpected(string userId, TimeProvider clock) => new() {
        ContractIdentifier = ContractId,
        CoopIdentifier = "wire-coop",
        Rinfo = Info(Updated, userId),
        UserId = userId,
        ClientVersion = Updated.ClientVersion,
        ClientTimestamp = clock.GetUtcNow().ToUnixTimeSeconds()
    };

    private static byte[] Wrap(byte[] inner) =>
        new AuthenticatedMessage { Message = ByteString.CopyFrom(inner), Code = Signer.Sign(inner) }.ToByteArray();

    private static async Task<byte[]> FormBody(byte[] payload) {
        using var content = new FormUrlEncodedContent([new("data", Convert.ToBase64String(payload))]);
        return await content.ReadAsByteArrayAsync();
    }

    private static byte[] Payload(WireRequest request) =>
        Convert.FromBase64String(Uri.UnescapeDataString(Encoding.ASCII.GetString(request.Body)["data=".Length..]));

    private enum ExpectedRinfo { None, WithUser, WithoutUser }

    private sealed record TableCase(string Profile, ExpectedRinfo Rinfo, bool Signed, Func<BasicRequestInfo?, IMessage> Build, Func<IEggIncApi, IMessage, Task> Send);
}

internal sealed record WireRequest(Uri Uri, Version Version, string[] Headers, string? ContentType, byte[] Body);

internal sealed class WireRecorder : HttpMessageHandler {
    public List<WireRequest> Requests { get; } = [];

    public Func<HttpRequestMessage, HttpResponseMessage>? Respond { get; set; }

    public static string[] Normalize(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers) =>
        [.. headers.Select(h => $"{h.Key}: {string.Join("|", h.Value)}").Order(StringComparer.Ordinal)];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        Requests.Add(new(request.RequestUri!, request.Version, Normalize(request.Headers), request.Content?.Headers.ContentType?.ToString(), body));
        return Respond?.Invoke(request) ?? new(HttpStatusCode.OK) { Content = new StringContent("") };
    }
}

internal sealed class WireHarness(ServiceProvider provider, WireRecorder recorder, VersionTripleSource versions) : IDisposable {
    public const string SaltPhrase = "eggsharp-wire-fixture";

    public IEggIncApi Api { get; } = provider.GetRequiredService<IEggIncApi>();

    public WireRecorder Recorder => recorder;

    public VersionTripleSource Versions => versions;

    public WireRequest Single => recorder.Requests.Single();

    public static WireHarness Create(VersionTriple triple, string? salt = SaltPhrase) {
        var recorder = new WireRecorder();
        var versions = new VersionTripleSource(triple);
        var services = new ServiceCollection();
        services.AddEggSharpApi(ApiPresets.Egg9000, _ => new GoogleEiCodec(), _ => new AuxbrainSigner(salt), _ => versions);
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = recorder));
        return new(services.BuildServiceProvider(), recorder, versions);
    }

    public void Dispose() => provider.Dispose();
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EggSharp.Api;
using EggSharp.Core;
using Ei;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using ProtoBuf;

namespace EggSharp.Ei.ProtobufNet.Tests.Wire;

public sealed class EggLedgerWireTests {
    private const string Origin = "https://ctx-dot-auxbrainhome.appspot.com/";
    private const string Player = "EI1111222233334444";

    private static readonly VersionTriple Ledger = new(72, "1.35.7", "111343");

    [Fact]
    public async Task EggLedger_BotFirstContact_MatchesApiClient() {
        var recorder = new LedgerRecorder();
        using var provider = Harness(recorder);

        await provider.GetRequiredService<IEggIncApi>().BotFirstContact(Player);

        var expected = new EggIncFirstContactRequest {
            Rinfo = Rinfo(Player),
            EiUserId = Player,
            DeviceId = "EggLedger",
            ClientVersion = Ledger.ClientVersion,
            Platform = Platform.Ios
        };
        await AssertRequest(recorder.Requests.Single(), "ei/bot_first_contact", Serialize(expected));
    }

    [Fact]
    public async Task EggLedger_CompleteMission_MatchesApiClient() {
        var recorder = new LedgerRecorder();
        using var provider = Harness(recorder);

        await provider.GetRequiredService<IEggIncApi>().CompleteMission(Mission());

        await AssertRequest(recorder.Requests.Single(), "ei_afx/complete_mission", Serialize(Mission()));
    }

    [Fact]
    public async Task EggLedger_RawCalls_ReturnBase64DecodedBodyLikeRequestRawPayload() {
        byte[] payload = [0x0a, 0x03, 0x45, 0x49, 0x31, 0xff, 0x00];
        var recorder = new LedgerRecorder { Body = Convert.ToBase64String(payload) };
        using var provider = Harness(recorder);
        var api = provider.GetRequiredService<IEggIncApi>();

        var first = await api.BotFirstContactRaw(Player);
        var mission = await api.CompleteMissionRaw(Mission());

        Assert.Equal(payload, first.Response);
        Assert.Equal(payload, mission.Response);
    }

    [Fact]
    public async Task EggLedger_ProxyOrigin_ResolvesAgainstHostBase() {
        var recorder = new LedgerRecorder();
        using var provider = Harness(recorder, new AuxbrainHosts("/egg-api", null, new Uri("https://ledger.test/app/")));

        await provider.GetRequiredService<IEggIncApi>().CompleteMission(Mission());

        Assert.Equal(new Uri("https://ledger.test/egg-api/ei_afx/complete_mission"), recorder.Requests.Single().Uri);
    }

    [Fact]
    public async Task EggLedger_Non2xx_CarriesNumericStatusAndBody() {
        var recorder = new LedgerRecorder { Status = HttpStatusCode.InternalServerError, Body = "server said no" };
        using var provider = Harness(recorder);

        var outcome = await provider.GetRequiredService<IEggIncApi>().CompleteMissionRaw(Mission());

        Assert.False(outcome.Success);
        Assert.Equal(500, outcome.StatusCode);
        Assert.Equal("server said no", outcome.ResponseBody);
    }

    [Fact]
    public async Task EggLedger_TransportFailure_PropagatesUnwrapped() {
        var recorder = new LedgerRecorder { Throw = new HttpRequestException("unreachable") };
        using var provider = Harness(recorder);

        var e = await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetRequiredService<IEggIncApi>().CompleteMissionRaw(Mission()));

        Assert.Equal("unreachable", e.Message);
    }

    [Fact]
    public async Task EggLedger_CallerCancellation_PropagatesAsCancellation() {
        var recorder = new LedgerRecorder();
        using var provider = Harness(recorder);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => provider.GetRequiredService<IEggIncApi>().CompleteMissionRaw(Mission(), null, cts.Token));
    }

    [Fact]
    public async Task EggLedger_MalformedBase64_ThrowsFormatException() {
        var recorder = new LedgerRecorder { Body = "not*base64" };
        using var provider = Harness(recorder);

        await Assert.ThrowsAsync<FormatException>(() => provider.GetRequiredService<IEggIncApi>().CompleteMissionRaw(Mission()));
    }

    private static MissionRequest Mission() => new() {
        Rinfo = Rinfo(Player),
        EiUserId = Player,
        Info = new MissionInfo { Identifier = "wire-mission" },
        ClientVersion = Ledger.ClientVersion
    };

    private static BasicRequestInfo Rinfo(string userId) => new() {
        EiUserId = userId,
        ClientVersion = Ledger.ClientVersion,
        Version = Ledger.AppVersion,
        Build = Ledger.AppBuild,
        Platform = "IOS"
    };

    private static byte[] Serialize<T>(T message) {
        using var buffer = new MemoryStream();
        Serializer.Serialize(buffer, message);
        return buffer.ToArray();
    }

    private static async Task AssertRequest(LedgerRequest actual, string path, byte[] payload) {
        using var form = new FormUrlEncodedContent([new("data", Convert.ToBase64String(payload))]);
        Assert.Equal(HttpMethod.Post, actual.Method);
        Assert.Equal(new Uri(Origin + path), actual.Uri);
        Assert.Empty(actual.Headers);
        Assert.Equal("application/x-www-form-urlencoded", actual.ContentType);
        Assert.Equal(await form.ReadAsByteArrayAsync(), actual.Body);
    }

    private static ServiceProvider Harness(LedgerRecorder recorder, AuxbrainHosts? hosts = null) {
        var services = new ServiceCollection();
        services.AddEggSharpApi(
            ApiPresets.EggLedger,
            _ => new ProtobufNetEiCodec(),
            _ => new AuxbrainSigner(null),
            _ => new VersionTripleSource(Ledger),
            _ => hosts ?? new AuxbrainHosts(Origin));
        services.ConfigureAll<HttpClientFactoryOptions>(o => o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = recorder));
        return services.BuildServiceProvider();
    }
}

internal sealed record LedgerRequest(HttpMethod Method, Uri Uri, string[] Headers, string? ContentType, byte[] Body);

internal sealed class LedgerRecorder : HttpMessageHandler {
    public List<LedgerRequest> Requests { get; } = [];

    public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

    public string Body { get; init; } = "";

    public Exception? Throw { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var headers = request.Headers.Select(h => $"{h.Key}: {string.Join("|", h.Value)}").ToArray();
        Requests.Add(new(request.Method, request.RequestUri!, headers, request.Content?.Headers.ContentType?.ToString(), body));
        if (Throw is not null)
            throw Throw;
        return new(Status) { Content = new StringContent(Body) };
    }
}

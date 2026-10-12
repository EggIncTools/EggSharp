using System.Net;
using System.Text;
using EggSharp.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace EggSharp.Api.Tests;

public sealed class FakeRequest(string body) {
    public string Body { get; } = body;

    public RinfoValues? Rinfo { get; set; }
}

public sealed class FakeResponse(byte[] bytes) {
    public byte[] Bytes { get; } = bytes;
}

public sealed class SpecialResponse(byte[] bytes) {
    public byte[] Bytes { get; } = bytes;
}

public sealed class FakeCodec : IEiCodec {
    private static readonly byte[] AuthPrefix = "AUTH:"u8.ToArray();

    public byte[] Serialize(object message) {
        var request = (FakeRequest)message;
        var rinfo = request.Rinfo is { } r ? $"|{r.UserId}|{r.ClientVersion}|{r.Version}|{r.Build}|{r.Platform}|{r.Country}|{r.Language}|{r.Debug}|{r.DeviceId}" : "";
        return Encoding.UTF8.GetBytes(request.Body + rinfo);
    }

    public object Parse(Type messageType, byte[] bytes) =>
        messageType == typeof(SpecialResponse) ? new SpecialResponse(bytes) : new FakeResponse(bytes);

    public byte[] WrapAuthenticated(byte[] message, string? code) => [.. AuthPrefix, .. Encoding.ASCII.GetBytes(code ?? ""), (byte)':', .. message];

    public bool TryUnwrapAuthenticated(byte[] bytes, out byte[] message, out bool compressed) {
        compressed = false;
        message = [];
        if (!bytes.AsSpan().StartsWith(AuthPrefix))
            return false;
        message = bytes[AuthPrefix.Length..];
        return true;
    }

    public void SetRinfo(object request, RinfoValues rinfo) => ((FakeRequest)request).Rinfo = rinfo;

    public string MessageName(Type messageType) => "ei." + messageType.Name;
}

public sealed class RecordingHandler(Func<int, HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler {
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<byte[]> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        Bodies.Add(request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken));
        return respond(Requests.Count, request);
    }

    public static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status) { Content = new StringContent("") };
}

public sealed class FakeHttpClientFactory(ApiPreset preset, HttpMessageHandler handler) : IHttpClientFactory {
    public HttpClient CreateClient(string name) {
        var client = new HttpClient(handler, false);
        var profile = preset.Profiles.Single(p => EggIncApiHttp.ClientName(p.Name) == name);
        EggIncApiHttp.Configure(client, profile);
        return client;
    }
}

public sealed class RecordingMetrics : IEggIncApiMetrics {
    public List<string> Started { get; } = [];

    public List<(string Path, Exception Exception)> Failed { get; } = [];

    public void CallStarted(string path) => Started.Add(path);

    public void CallFailed(string path, Exception exception) => Failed.Add((path, exception));
}

public sealed class CountingLogger<T> : ILogger<T> {
    public int Errors { get; private set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        if (logLevel == LogLevel.Error)
            Errors++;
    }
}

internal static class ApiFixture {
    public const string Origin = "https://auxbrain.test";

    public static readonly VersionTriple Versions = new(75, "1.35.6", "111343");

    public static readonly EndpointSpec Plain = new("ei/plain", "Test", RinfoMode.WithUser, false, false);

    public static readonly EndpointSpec Signed = new("ei/signed", "Test", RinfoMode.WithUser, true, false);

    public static ApiPreset Preset(Func<ResiliencePipelineBuilder, ResiliencePipelineBuilder>? retry = null) => new(
        "Test",
        [new HeaderProfile("Test", "test-agent", new Dictionary<string, string>(), HttpVersion.Version11, null, TimeSpan.FromSeconds(5), false, null)],
        new RinfoDefaults("IOS", null, null, null, null, "IOS", false),
        new Dictionary<(string Request, string? Response), EndpointSpec> {
            [("ei.FakeRequest", "ei.SpecialResponse")] = new("ei/pair", "Test", RinfoMode.None, false, false),
            [("ei.FakeRequest", null)] = new("ei/fallback", "Test", RinfoMode.None, false, false)
        },
        retry,
        ApiPresets.DefaultErrors);

    public static ResiliencePipelineBuilder ImmediateRetry(ResiliencePipelineBuilder builder) => builder.AddRetry(new RetryStrategyOptions {
        MaxRetryAttempts = 2,
        Delay = TimeSpan.Zero,
        ShouldHandle = new PredicateBuilder()
            .Handle<HttpRequestException>()
            .HandleResult(r => r is HttpResponseMessage { StatusCode: >= HttpStatusCode.InternalServerError })
    });

    public static EggIncApi Create(ApiPreset preset, RecordingHandler handler, RecordingMetrics? metrics = null, CountingLogger<EggIncApi>? logger = null, string? salt = "salt") =>
        new(
            new FakeHttpClientFactory(preset, handler),
            new EggIncApiContext(preset, new FakeCodec(), new AuxbrainSigner(salt), new VersionTripleSource(Versions), new AuxbrainHosts(Origin)),
            EggIncApi.BuildPipeline(preset, TimeProvider.System),
            metrics,
            logger ?? new CountingLogger<EggIncApi>());

    public static string Encoded(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
}

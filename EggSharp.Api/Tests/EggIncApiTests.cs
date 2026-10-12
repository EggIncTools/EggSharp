using System.Net;
using System.Text;

namespace EggSharp.Api.Tests;

public sealed class EggIncApiTests {
    [Fact]
    public async Task Post_RequestResponsePair_BeatsRequestOnly() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok(ApiFixture.Encoded("x")));
        var api = ApiFixture.Create(ApiFixture.Preset(), handler);
        await api.Post<FakeRequest, SpecialResponse>(new FakeRequest("a"));
        Assert.Equal(new Uri(ApiFixture.Origin + "/ei/pair"), handler.Requests.Single().RequestUri);
    }

    [Fact]
    public async Task Post_UnpairedResponse_FallsBackToRequestOnly() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok(ApiFixture.Encoded("x")));
        var api = ApiFixture.Create(ApiFixture.Preset(), handler);
        await api.Post<FakeRequest, FakeResponse>(new FakeRequest("a"));
        Assert.Equal(new Uri(ApiFixture.Origin + "/ei/fallback"), handler.Requests.Single().RequestUri);
    }

    [Fact]
    public async Task Post_ParsesBase64Body() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok(ApiFixture.Encoded("payload")));
        var response = await ApiFixture.Create(ApiFixture.Preset(), handler).Post<FakeRequest, FakeResponse>(new FakeRequest("a"));
        Assert.Equal("payload", Encoding.UTF8.GetString(response!.Bytes));
    }

    [Fact]
    public async Task Post_Non2xx_ReturnsDefault() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Status(HttpStatusCode.NotFound));
        Assert.Null(await ApiFixture.Create(ApiFixture.Preset(), handler).Post<FakeRequest, FakeResponse>(new FakeRequest("a")));
    }

    [Fact]
    public async Task Post_Exception_ReturnsDefault() {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("down"));
        Assert.Null(await ApiFixture.Create(ApiFixture.Preset(), handler).Post<FakeRequest, FakeResponse>(new FakeRequest("a")));
    }

    [Fact]
    public async Task PostResult_Non2xx_IsHttpError() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Status(HttpStatusCode.NotFound));
        var result = await ApiFixture.Create(ApiFixture.Preset(), handler).PostResult<FakeRequest, FakeResponse>(new FakeRequest("a"));
        Assert.Equal("HTTP Error: NotFound", result.Error);
    }

    [Fact]
    public async Task PostResult_EmptyBody_DecodesEmptyMessage() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok(""));
        var result = await ApiFixture.Create(ApiFixture.Preset(), handler).PostResult<FakeRequest, FakeResponse>(new FakeRequest("a"));
        Assert.True(result.Success);
        Assert.Empty(result.Value!.Bytes);
    }

    [Fact]
    public async Task CallRaw_Non2xx_CarriesStatusCodeAndBody() {
        var handler = new RecordingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") });
        var outcome = await ApiFixture.Create(ApiFixture.Preset(), handler).CallRaw(ApiFixture.Plain, new FakeRequest("a"));
        Assert.False(outcome.Success);
        Assert.Equal(500, outcome.StatusCode);
        Assert.Equal("boom", outcome.ResponseBody);
        Assert.Equal("HTTP Error: InternalServerError", outcome.Error);
    }

    [Fact]
    public async Task CallRaw_ReturnsBase64DecodedBodyBytes() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok(ApiFixture.Encoded("raw-bytes")));
        var outcome = await ApiFixture.Create(ApiFixture.Preset(), handler).CallRaw(ApiFixture.Plain, new FakeRequest("a"));
        Assert.Equal("raw-bytes"u8.ToArray(), outcome.Response);
    }

    [Fact]
    public async Task CallRaw_TransportException_PropagatesForConsumerMapping() {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("down"));
        await Assert.ThrowsAsync<HttpRequestException>(() => ApiFixture.Create(ApiFixture.Preset(), handler).CallRaw(ApiFixture.Plain, new FakeRequest("a")));
    }

    [Fact]
    public async Task CallRaw_MalformedBase64_ThrowsFormatException() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok("not*base64"));
        await Assert.ThrowsAsync<FormatException>(() => ApiFixture.Create(ApiFixture.Preset(), handler).CallRaw(ApiFixture.Plain, new FakeRequest("a")));
    }

    [Fact]
    public async Task Call_AuthenticatedEnvelopeWithoutMessage_DecodesEmptyMessage() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok(ApiFixture.Encoded("AUTH:")));
        var outcome = await ApiFixture.Create(ApiFixture.Preset(), handler).Call<FakeRequest, FakeResponse>(ApiFixture.Plain, new FakeRequest("a"), new ApiCallOptions(AuthenticatedResponse: true));
        Assert.True(outcome.Success);
        Assert.Empty(outcome.Response!.Bytes);
    }

    [Fact]
    public async Task PostResult_Exception_IsBotException() {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("down"));
        var result = await ApiFixture.Create(ApiFixture.Preset(), handler).PostResult<FakeRequest, FakeResponse>(new FakeRequest("a"));
        Assert.Equal("Bot Exception: down", result.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public async Task Send_ReturnsStatusSuccess(HttpStatusCode status, bool expected) {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Status(status));
        Assert.Equal(expected, await ApiFixture.Create(ApiFixture.Preset(), handler).Send(new FakeRequest("a")));
    }

    [Fact]
    public async Task Send_Exception_ReturnsFalse() {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("down"));
        Assert.False(await ApiFixture.Create(ApiFixture.Preset(), handler).Send(new FakeRequest("a")));
    }

    [Fact]
    public async Task Metrics_OneStartPerLogicalCall_DespiteRetries() {
        var handler = new RecordingHandler((n, _) => n < 3 ? RecordingHandler.Status(HttpStatusCode.ServiceUnavailable) : RecordingHandler.Ok(ApiFixture.Encoded("x")));
        var metrics = new RecordingMetrics();
        var api = ApiFixture.Create(ApiFixture.Preset(ApiFixture.ImmediateRetry), handler, metrics);
        var outcome = await api.Call<FakeRequest, FakeResponse>(ApiFixture.Plain, new FakeRequest("a"));
        Assert.True(outcome.Success);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal("ei/plain", Assert.Single(metrics.Started));
        Assert.Empty(metrics.Failed);
    }

    [Fact]
    public async Task Metrics_FailureCountedOnceAfterRetriesExhausted() {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("down"));
        var metrics = new RecordingMetrics();
        var api = ApiFixture.Create(ApiFixture.Preset(ApiFixture.ImmediateRetry), handler, metrics);
        await Assert.ThrowsAsync<HttpRequestException>(() => api.Call<FakeRequest, FakeResponse>(ApiFixture.Plain, new FakeRequest("a")));
        Assert.Equal(3, handler.Requests.Count);
        Assert.Single(metrics.Started);
        Assert.IsType<HttpRequestException>(Assert.Single(metrics.Failed).Exception);
    }

    [Fact]
    public async Task Metrics_Non2xx_IsNotFailure() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Status(HttpStatusCode.NotFound));
        var metrics = new RecordingMetrics();
        var outcome = await ApiFixture.Create(ApiFixture.Preset(), handler, metrics).Call<FakeRequest, FakeResponse>(ApiFixture.Plain, new FakeRequest("a"));
        Assert.Equal("HTTP Error: NotFound", outcome.Error);
        Assert.Empty(metrics.Failed);
    }

    [Fact]
    public async Task Metrics_CallerCancellation_IsNotFailure() {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok(ApiFixture.Encoded("x")));
        var metrics = new RecordingMetrics();
        var api = ApiFixture.Create(ApiFixture.Preset(), handler, metrics);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.Call<FakeRequest, FakeResponse>(ApiFixture.Plain, new FakeRequest("a"), cancellationToken: cts.Token));
        Assert.Single(metrics.Started);
        Assert.Empty(metrics.Failed);
    }

    [Fact]
    public async Task Call_RethrowsTransportExceptionAfterRetries() {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("down"));
        var api = ApiFixture.Create(ApiFixture.Preset(ApiFixture.ImmediateRetry), handler);
        var e = await Assert.ThrowsAsync<HttpRequestException>(() => api.Call<FakeRequest, FakeResponse>(ApiFixture.Plain, new FakeRequest("a")));
        Assert.Equal("down", e.Message);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Call_SignedWithoutSalt_ReturnsNoSaltOutcomeAndLogsOnce() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok(ApiFixture.Encoded("x")));
        var logger = new CountingLogger<EggIncApi>();
        var api = ApiFixture.Create(ApiFixture.Preset(), handler, logger: logger, salt: null);
        var first = await api.Call<FakeRequest, FakeResponse>(ApiFixture.Signed, new FakeRequest("a"));
        var second = await api.Call<FakeRequest, FakeResponse>(ApiFixture.Signed, new FakeRequest("a"));
        Assert.False(first.Success);
        Assert.Equal("API salt not configured for ei/signed", first.Error);
        Assert.Equal(first, second);
        Assert.Equal(1, logger.Errors);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Call_RinfoWithUser_CarriesCallerUserAndVersions() {
        var handler = new RecordingHandler((_, _) => RecordingHandler.Ok(ApiFixture.Encoded("x")));
        var request = new FakeRequest("a");
        await ApiFixture.Create(ApiFixture.Preset(), handler).Call<FakeRequest, FakeResponse>(ApiFixture.Plain, request, new ApiCallOptions(UserId: "EI1234567890123456"));
        Assert.Equal(new RinfoValues("EI1234567890123456", 75, "1.35.6", "111343", "IOS", null, null, null, null), request.Rinfo);
    }
}

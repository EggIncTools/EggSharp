using System.Net;

namespace EggSharp.Testing.Tests;

public sealed class StubAuxbrainHandlerTests {
    private static readonly Uri Host = new("https://auxbrain.test/");

    [Fact]
    public async Task ServesCannedBytesByPath() {
        using var handler = new StubAuxbrainHandler().Serve("/ei/get_periodicals", [1, 2, 3]);
        using var client = new HttpClient(handler) { BaseAddress = Host };

        using var response = await client.PostAsync("ei/get_periodicals", new ByteArrayContent([9]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new byte[] { 1, 2, 3 }, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ServesUnderAProxyPrefix() {
        using var handler = new StubAuxbrainHandler(new Dictionary<string, byte[]> { ["ei/bot_first_contact"] = [7] });
        using var client = new HttpClient(handler) { BaseAddress = Host };

        using var response = await client.PostAsync("egg-api/ei/bot_first_contact", null);

        Assert.Equal(new byte[] { 7 }, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UnknownPathReturnsMissingStatus() {
        using var handler = new StubAuxbrainHandler { MissingStatus = HttpStatusCode.InternalServerError };
        using var client = new HttpClient(handler) { BaseAddress = Host };

        using var response = await client.PostAsync("ei/nope", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task RecordsPathBodyAndHeaders() {
        using var handler = new StubAuxbrainHandler();
        using var client = new HttpClient(handler) { BaseAddress = Host };
        using var request = new HttpRequestMessage(HttpMethod.Post, "ei/coop_status?x=1") { Content = new StringContent("data=abc") };
        request.Headers.UserAgent.ParseAdd("egginc/1.35.3.1");

        using var _ = await client.SendAsync(request);

        var recorded = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, recorded.Method);
        Assert.Equal("ei/coop_status", recorded.Path);
        Assert.Equal("?x=1", recorded.Query);
        Assert.Equal("data=abc"u8.ToArray(), recorded.Body);
        Assert.Equal("egginc/1.35.3.1", recorded.Headers["user-agent"]);
        Assert.StartsWith("text/plain", recorded.Headers["Content-Type"], StringComparison.Ordinal);
    }
}

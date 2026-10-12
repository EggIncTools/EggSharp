using EggSharp.Api;

namespace EggSharp.Testing.Tests;

public sealed class FakeEggIncApiTests {
    private sealed record PingRequest(string Id);

    private sealed record PingResponse(string Echo);

    private sealed record OtherRequest;

    private static readonly EndpointSpec Endpoint = new("ei/ping", "Android", RinfoMode.WithUser, false, true);

    [Fact]
    public async Task Post_ReturnsCannedResponseForRequestType() {
        var api = new FakeEggIncApi().Respond<PingRequest, PingResponse>(r => new PingResponse(r!.Id));

        var response = await api.Post<PingRequest, PingResponse>(new PingRequest("a"));

        Assert.Equal("a", response?.Echo);
    }

    [Fact]
    public async Task Post_ReturnsNullForUnregisteredRequestType() =>
        Assert.Null(await new FakeEggIncApi().Post<OtherRequest, PingResponse>(new OtherRequest()));

    [Fact]
    public async Task PostResult_FailsWithNoResponseWhenUnregistered() {
        var result = await new FakeEggIncApi().PostResult<OtherRequest, PingResponse>(new OtherRequest());

        Assert.False(result.Success);
        Assert.Equal(FakeEggIncApi.NoResponse, result.Error);
    }

    [Fact]
    public async Task PostResult_CarriesTheCannedValue() {
        var result = await new FakeEggIncApi().Respond<PingRequest, PingResponse>(new PingResponse("x"))
            .PostResult<PingRequest, PingResponse>(new PingRequest("a"));

        Assert.True(result.Success);
        Assert.Equal("x", result.Value?.Echo);
    }

    [Fact]
    public async Task Send_IsTrueOnlyForRegisteredRequestTypes() {
        var api = new FakeEggIncApi().Respond<PingRequest, PingResponse>(new PingResponse("x"));

        Assert.True(await api.Send(new PingRequest("a")));
        Assert.False(await api.Send(new OtherRequest()));
    }

    [Fact]
    public async Task Call_ReturnsOutcomeAndRecordsEndpoint() {
        var api = new FakeEggIncApi().Respond<PingRequest, PingResponse>(new PingResponse("x"));
        var options = new ApiCallOptions(Profile: "iOS");

        var ok = await api.Call<PingRequest, PingResponse>(Endpoint, new PingRequest("a"), options);
        var missing = await api.Call<OtherRequest, PingResponse>(Endpoint, null);

        Assert.True(ok.Success);
        Assert.Equal("x", ok.Response?.Echo);
        Assert.False(missing.Success);
        Assert.Equal(FakeEggIncApi.NoResponse, missing.Error);
        Assert.Equal(Endpoint, api.Calls[0].Endpoint);
        Assert.Same(options, api.Calls[0].Options);
    }

    [Fact]
    public async Task Calls_RecordsEveryInvocationInOrder() {
        var api = new FakeEggIncApi();
        var request = new PingRequest("a");

        await api.Post<PingRequest, PingResponse>(request);
        await api.Send(new OtherRequest());

        Assert.Collection(api.Calls,
            c => {
                Assert.Equal(nameof(IEggIncApi.Post), c.Method);
                Assert.Same(request, c.Request);
                Assert.Equal(typeof(PingResponse), c.ResponseType);
            },
            c => {
                Assert.Equal(nameof(IEggIncApi.Send), c.Method);
                Assert.Equal(typeof(OtherRequest), c.RequestType);
                Assert.Null(c.ResponseType);
            });
    }

    [Fact]
    public async Task Post_HonoursCancellation() {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new FakeEggIncApi().Post<PingRequest, PingResponse>(new PingRequest("a"), cancellationToken: cts.Token));
    }
}

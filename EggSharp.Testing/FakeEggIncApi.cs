using System.Collections.Concurrent;
using EggSharp.Api;
using EggSharp.Core;

namespace EggSharp.Testing;

public sealed record FakeApiCall(string Method, Type RequestType, Type? ResponseType, object? Request, ApiCallOptions? Options, EndpointSpec? Endpoint);

public sealed class FakeEggIncApi : IEggIncApi {
    public const string NoResponse = "No response";

    private readonly ConcurrentDictionary<Type, Func<object?, object?>> _responses = new();
    private readonly ConcurrentQueue<FakeApiCall> _calls = new();

    public IReadOnlyList<FakeApiCall> Calls => [.. _calls];

    public VersionTriple Versions { get; set; } = new(0, "0", "0");

    public ApiPreset Preset { get; set; } = ApiPresets.Egg9000;

    public bool CanSign { get; set; } = true;

    public FakeEggIncApi Respond<TRequest, TResponse>(TResponse response) where TRequest : class where TResponse : class =>
        Respond<TRequest, TResponse>(_ => response);

    public FakeEggIncApi Respond<TRequest, TResponse>(Func<TRequest?, TResponse?> respond) where TRequest : class where TResponse : class {
        _responses[typeof(TRequest)] = request => respond(request as TRequest);
        return this;
    }

    public Task<TResponse?> Post<TRequest, TResponse>(TRequest request, ApiCallOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : class where TResponse : class {
        cancellationToken.ThrowIfCancellationRequested();
        Record(nameof(Post), typeof(TRequest), typeof(TResponse), request, options, null);
        return Task.FromResult(Resolve<TResponse>(typeof(TRequest), request));
    }

    public Task<ApiResult<TResponse>> PostResult<TRequest, TResponse>(TRequest request, ApiCallOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : class where TResponse : class {
        cancellationToken.ThrowIfCancellationRequested();
        Record(nameof(PostResult), typeof(TRequest), typeof(TResponse), request, options, null);
        return Task.FromResult(Resolve<TResponse>(typeof(TRequest), request) is { } response
            ? new ApiResult<TResponse>(response, null)
            : ApiResult.Fail<TResponse>(NoResponse));
    }

    public Task<bool> Send<TRequest>(TRequest request, ApiCallOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : class {
        cancellationToken.ThrowIfCancellationRequested();
        Record(nameof(Send), typeof(TRequest), null, request, options, null);
        return Task.FromResult(_responses.ContainsKey(typeof(TRequest)));
    }

    public Task<ApiOutcome<TResponse>> Call<TRequest, TResponse>(EndpointSpec endpoint, TRequest? request, ApiCallOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : class where TResponse : class {
        cancellationToken.ThrowIfCancellationRequested();
        Record(nameof(Call), typeof(TRequest), typeof(TResponse), request, options, endpoint);
        return Task.FromResult(Resolve<TResponse>(typeof(TRequest), request) is { } response
            ? ApiOutcome.Ok(response)
            : ApiOutcome.Fail<TResponse>(NoResponse));
    }

    public Task<ApiOutcome<byte[]>> CallRaw<TRequest>(EndpointSpec endpoint, TRequest? request, ApiCallOptions? options = null, CancellationToken cancellationToken = default)
        where TRequest : class {
        cancellationToken.ThrowIfCancellationRequested();
        Record(nameof(CallRaw), typeof(TRequest), typeof(byte[]), request, options, endpoint);
        return Task.FromResult(Resolve<byte[]>(typeof(TRequest), request) is { } response
            ? ApiOutcome.Ok(response)
            : ApiOutcome.Fail<byte[]>(NoResponse));
    }

    private TResponse? Resolve<TResponse>(Type requestType, object? request) where TResponse : class =>
        _responses.TryGetValue(requestType, out var respond) ? respond(request) as TResponse : null;

    private void Record(string method, Type requestType, Type? responseType, object? request, ApiCallOptions? options, EndpointSpec? endpoint) =>
        _calls.Enqueue(new FakeApiCall(method, requestType, responseType, request, options, endpoint));
}

using EggSharp.Core;

namespace EggSharp.Api;

public interface IEggIncApi {
    VersionTriple Versions { get; }

    ApiPreset Preset { get; }

    bool CanSign { get; }

    Task<TResponse?> Post<TRequest, TResponse>(TRequest request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class where TResponse : class;

    Task<ApiResult<TResponse>> PostResult<TRequest, TResponse>(TRequest request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class where TResponse : class;

    Task<bool> Send<TRequest>(TRequest request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class;

    Task<ApiOutcome<TResponse>> Call<TRequest, TResponse>(EndpointSpec endpoint, TRequest? request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class where TResponse : class;

    Task<ApiOutcome<byte[]>> CallRaw<TRequest>(EndpointSpec endpoint, TRequest? request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class;
}

public interface IEggIncApiMetrics {
    void CallStarted(string path);

    void CallFailed(string path, Exception exception);
}

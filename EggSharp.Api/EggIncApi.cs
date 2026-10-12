using System.Net.Http.Headers;
using System.Text;
using EggSharp.Core;
using Microsoft.Extensions.Logging;
using Polly;

namespace EggSharp.Api;

internal sealed class EggIncApi(IHttpClientFactory httpClientFactory, EggIncApiContext context, ResiliencePipeline pipeline, IEggIncApiMetrics? metrics, ILogger<EggIncApi> logger) : IEggIncApi {
    private int _saltWarned;

    public static ResiliencePipeline BuildPipeline(ApiPreset preset, TimeProvider timeProvider) =>
        preset.Retry is { } retry ? retry(new ResiliencePipelineBuilder { TimeProvider = timeProvider }).Build() : ResiliencePipeline.Empty;

    private ApiErrorText Errors => context.Preset.Errors;

    public VersionTriple Versions => context.Versions.Current;

    public ApiPreset Preset => context.Preset;

    public bool CanSign => context.Signer.CanSign;

    private string ResolveProfile(string name) =>
        context.Preset.Profiles.Any(p => p.Name == name) ? name : context.Preset.Profiles[0].Name;

    public async Task<TResponse?> Post<TRequest, TResponse>(TRequest request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class where TResponse : class {
        var endpoint = Resolve(typeof(TRequest), typeof(TResponse));
        try {
            var outcome = await Call<TRequest, TResponse>(endpoint, request, options, cancellationToken);
            return outcome.Success ? outcome.Response : null;
        } catch (Exception) {
            return null;
        }
    }

    public async Task<ApiResult<TResponse>> PostResult<TRequest, TResponse>(TRequest request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class where TResponse : class {
        var endpoint = Resolve(typeof(TRequest), typeof(TResponse));
        try {
            var outcome = await Call<TRequest, TResponse>(endpoint, request, options, cancellationToken);
            return outcome.Success ? new ApiResult<TResponse>(outcome.Response, null) : ApiResult.Fail<TResponse>(outcome.Error ?? Errors.NoResponse);
        } catch (Exception e) {
            return ApiResult.Fail<TResponse>(Fill(Errors.BotException, e.Message));
        }
    }

    public async Task<bool> Send<TRequest>(TRequest request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class {
        var endpoint = Resolve(typeof(TRequest), null);
        if (!TryBuildBody(endpoint, request, options, out var body))
            return false;
        try {
            using var response = await Exchange(endpoint, options, body, cancellationToken);
            return response.IsSuccessStatusCode;
        } catch (Exception) {
            return false;
        }
    }

    public async Task<ApiOutcome<TResponse>> Call<TRequest, TResponse>(EndpointSpec endpoint, TRequest? request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class where TResponse : class {
        var raw = await CallRaw(endpoint, request, options, cancellationToken);
        if (raw is not { Success: true, Response: { } payload })
            return new(default, false, raw.Error, raw.StatusCode, raw.ResponseBody);
        var authenticated = options?.AuthenticatedResponse ?? endpoint.AuthenticatedResponse;
        return ApiOutcome.Ok((TResponse)Decode(typeof(TResponse), payload, authenticated));
    }

    public async Task<ApiOutcome<byte[]>> CallRaw<TRequest>(EndpointSpec endpoint, TRequest? request, ApiCallOptions? options = null, CancellationToken cancellationToken = default) where TRequest : class {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!TryBuildBody(endpoint, request, options, out var body))
            return ApiOutcome.Fail<byte[]>(Fill(Errors.NoSalt, endpoint.Path));
        using var response = await Exchange(endpoint, options, body, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new(null, false, Fill(Errors.HttpError, response.StatusCode.ToString()), (int)response.StatusCode, text);
        return ApiOutcome.Ok(Convert.FromBase64String(text));
    }

    private EndpointSpec Resolve(Type request, Type? response) {
        var requestName = context.Codec.MessageName(request);
        var endpoints = context.Preset.Endpoints;
        if (response is not null && endpoints.TryGetValue((requestName, context.Codec.MessageName(response)), out var pair))
            return pair;
        return endpoints.TryGetValue((requestName, null), out var fallback)
            ? fallback
            : throw new InvalidOperationException($"Missing endpoint for {requestName}");
    }

    private bool TryBuildBody(EndpointSpec endpoint, object? request, ApiCallOptions? options, out byte[]? body) {
        body = null;
        if (endpoint.SignedRequest && !context.Signer.CanSign) {
            if (Interlocked.Exchange(ref _saltWarned, 1) == 0)
                logger.LogError("Salt not configured; signed endpoint {Path} is disabled.", endpoint.Path);
            return false;
        }
        if (request is null)
            return true;
        if (endpoint.Rinfo != RinfoMode.None)
            context.Codec.SetRinfo(request, Rinfo(endpoint.Rinfo, options));
        var message = context.Codec.Serialize(request);
        if (endpoint.SignedRequest)
            message = context.Codec.WrapAuthenticated(message, context.Signer.Sign(message));
        body = Encoding.ASCII.GetBytes(AuxbrainFraming.FormBody(message));
        return true;
    }

    private RinfoValues Rinfo(RinfoMode mode, ApiCallOptions? options) {
        var versions = options?.Versions ?? context.Versions.Current;
        var defaults = context.Preset.Rinfo;
        var userId = mode == RinfoMode.WithUser ? options?.UserId : null;
        return new(userId, versions.ClientVersion, versions.AppVersion, versions.AppBuild, defaults.Platform, defaults.Country, defaults.Language, defaults.Debug, defaults.DeviceId);
    }

    private async Task<HttpResponseMessage> Exchange(EndpointSpec endpoint, ApiCallOptions? options, byte[]? body, CancellationToken cancellationToken) {
        var client = httpClientFactory.CreateClient(EggIncApiHttp.ClientName(ResolveProfile(options?.Profile ?? endpoint.Profile)));
        var uri = context.Hosts.Resolve(endpoint.Path);
        metrics?.CallStarted(endpoint.Path);
        try {
            return await pipeline.ExecuteAsync(async token => {
                using var content = FormContent(body);
                return await client.PostAsync(uri, content, token);
            }, cancellationToken);
        } catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested) {
            metrics?.CallFailed(endpoint.Path, e);
            (options?.Logger ?? logger).LogWarning(e, "Egg Inc API call to {Path} failed after retries", endpoint.Path);
            throw;
        }
    }

    private object Decode(Type type, byte[] bytes, bool authenticated) {
        if (!authenticated)
            return context.Codec.Parse(type, bytes);
        if (!context.Codec.TryUnwrapAuthenticated(bytes, out var message, out var compressed))
            throw new InvalidDataException("Response is not an AuthenticatedMessage.");
        return context.Codec.Parse(type, compressed ? AuxbrainFraming.Decompress(message) : message);
    }

    private static ByteArrayContent? FormContent(byte[]? body) =>
        body is null ? null : new(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded") } };

    private static string Fill(string template, string value) => template.Replace("{0}", value, StringComparison.Ordinal);
}

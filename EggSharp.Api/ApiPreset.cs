using EggSharp.Core;
using Microsoft.Extensions.Logging;
using Polly;

namespace EggSharp.Api;

public enum RinfoMode { None, WithUser, WithoutUser }

public sealed record HeaderProfile(string Name, string? UserAgent, IReadOnlyDictionary<string, string> Headers, Version HttpVersion, string? Cookie, TimeSpan Timeout, bool AutomaticDecompression, TimeSpan? PooledConnectionLifetime);

public sealed record EndpointSpec(string Path, string Profile, RinfoMode Rinfo, bool SignedRequest, bool AuthenticatedResponse);

public sealed record ApiPreset(
    string Name,
    IReadOnlyList<HeaderProfile> Profiles,
    RinfoDefaults Rinfo,
    IReadOnlyDictionary<(string Request, string? Response), EndpointSpec> Endpoints,
    Func<ResiliencePipelineBuilder, ResiliencePipelineBuilder>? Retry,
    ApiErrorText Errors);

public sealed record RinfoDefaults(string Platform, string? Country, string? Language, bool? Debug, string? DeviceId, string FirstContactPlatform, bool FirstContactUsername);

public sealed record ApiErrorText(string HttpError, string NoResponse, string BotException, string ErrorResponse, string NoBackup, string NoSalt, string FirstContactFailed);

public sealed record ApiCallOptions(string? Profile = null, bool? AuthenticatedResponse = null, ILogger? Logger = null, VersionTriple? Versions = null, string? UserId = null);

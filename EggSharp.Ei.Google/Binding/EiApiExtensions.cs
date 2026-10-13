#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using EggSharp.Core;
using Ei;
using Microsoft.Extensions.Logging;

namespace EggSharp.Api;

internal static class EiApiExtensions {
    private static ApiErrorText Errors => ApiPresets.DefaultErrors;

    public static Task<ApiOutcome<EggIncFirstContactResponse>> BotFirstContact(
        this IEggIncApi api,
        string eiUserId,
        Platform? platform = null,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.Call<EggIncFirstContactRequest, EggIncFirstContactResponse>(
            EiEndpoints.BotFirstContact,
            FirstContactRequest(api, eiUserId, platform, options),
            WithUser(options, eiUserId),
            cancellationToken);
    }

    public static async Task<ApiOutcome<EggIncFirstContactResponse>> FirstContact(
        this IEggIncApi api,
        string userId,
        Platform? platform = null,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(userId);
        string botError;
        try {
            var bot = await api.BotFirstContact(userId, platform, options, cancellationToken);
            if (bot is { Success: true, Response.Backup: not null }) {
                return bot;
            }
            botError = bot.Success ? Errors.NoBackup : HttpOrError(bot);
        } catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested) {
            botError = Fill(Errors.BotException, e.Message);
        }
        if (!api.CanSign) {
            options?.Logger?.BotFirstContactNoSalt(userId, botError);
            return ApiOutcome.Fail<EggIncFirstContactResponse>(botError);
        }
        options?.Logger?.BotFirstContactFallback(botError);
        try {
            var ei = userId.StartsWith("EI", StringComparison.Ordinal);
            var request = ei
                ? FirstContactRequest(api, userId, platform, options)
                : new EggIncFirstContactRequest {
                    ClientVersion = (options?.Versions ?? api.Versions).ClientVersion,
                    Platform = platform ?? PresetPlatform(api),
                    UserId = userId
                };
            var outcome = await api.Call<EggIncFirstContactRequest, EggIncFirstContactResponse>(
                EiEndpoints.FirstContact(ei),
                request,
                WithUser(options, userId),
                cancellationToken);
            if (outcome.Success) {
                return outcome;
            }
            var error = HttpOrError(outcome);
            options?.Logger?.FirstContactSecureFailed(userId, error);
            return ApiOutcome.Fail<EggIncFirstContactResponse>(error);
        } catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested) {
            return ApiOutcome.Fail<EggIncFirstContactResponse>(Fill(Errors.BotException, e.Message));
        }
    }

    public static Task<ApiOutcome<byte[]>> BotFirstContactRaw(
        this IEggIncApi api,
        string eiUserId,
        Platform? platform = null,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.CallRaw(
            EiEndpoints.BotFirstContact,
            FirstContactRequest(api, eiUserId, platform, options),
            WithUser(options, eiUserId),
            cancellationToken);
    }

    public static Task<ApiOutcome<byte[]>> CompleteMissionRaw(
        this IEggIncApi api,
        MissionRequest request,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.CallRaw(EiEndpoints.CompleteMission, request, options, cancellationToken);
    }

    public static Task<ApiOutcome<CompleteMissionResponse>> CompleteMission(
        this IEggIncApi api,
        MissionRequest request,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.Call<MissionRequest, CompleteMissionResponse>(EiEndpoints.CompleteMission, request, options, cancellationToken);
    }

    public static Task<ApiOutcome<ContractCoopStatusResponse>> GetCoopStatus(
        this IEggIncApi api,
        string contractId,
        string coopId,
        string? userId = null,
        TimeProvider? timeProvider = null,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        return CoopStatus(api, EiEndpoints.CoopStatus, contractId, coopId, userId ?? options?.UserId, timeProvider, options, cancellationToken);
    }

    public static Task<ApiOutcome<ContractCoopStatusResponse>> GetCoopStatusBot(
        this IEggIncApi api,
        string contractId,
        string coopId,
        string botUserId,
        TimeProvider? timeProvider = null,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        return CoopStatus(api, EiEndpoints.CoopStatusBot, contractId, coopId, botUserId, timeProvider, options, cancellationToken);
    }

    public static Task<ApiOutcome<PeriodicalsResponse>> GetPeriodicals(
        this IEggIncApi api,
        GetPeriodicalsRequest request,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.Call<GetPeriodicalsRequest, PeriodicalsResponse>(EiEndpoints.Periodicals, request, options, cancellationToken);
    }

    public static async Task<bool> ValidateVersions(
        this IEggIncApi api,
        VersionTriple candidate,
        GetPeriodicalsRequest request,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        try {
            var outcome = await api.Call<GetPeriodicalsRequest, PeriodicalsResponse>(
                EiEndpoints.Periodicals,
                request,
                (options ?? new ApiCallOptions()) with { Versions = candidate },
                cancellationToken);
            return outcome is { Success: true, Response.Contracts.Contracts.Count: > 0 };
        } catch (Exception) {
            return false;
        }
    }

    public static Task<ApiOutcome<ContractsInfoResponse>> GetContractsInfo(
        this IEggIncApi api,
        ContractsInfoRequest request,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.Call<ContractsInfoRequest, ContractsInfoResponse>(EiEndpoints.ContractsInfo, request, options, cancellationToken);
    }

    public static Task<ApiOutcome<ContractPlayerInfo>> GetContractPlayerInfo(
        this IEggIncApi api,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.Call<BasicRequestInfo, ContractPlayerInfo>(EiEndpoints.ContractPlayerInfo, new BasicRequestInfo(), options, cancellationToken);
    }

    public static Task<ApiOutcome<ContractsArchive>> GetContractsArchive(
        this IEggIncApi api,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.Call<BasicRequestInfo, ContractsArchive>(EiEndpoints.ContractsArchive, new BasicRequestInfo(), options, cancellationToken);
    }

    public static Task<ApiOutcome<ContractSeasonInfos>> GetSeasonInfos(
        this IEggIncApi api,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.Call<BasicRequestInfo, ContractSeasonInfos>(EiEndpoints.SeasonInfos, new BasicRequestInfo(), options, cancellationToken);
    }

    public static Task<ApiOutcome<UserSubscriptionInfo>> GetUserSubscription(
        this IEggIncApi api,
        string userId,
        ApiCallOptions? options = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(api);
        return api.Call<BasicRequestInfo, UserSubscriptionInfo>(EiEndpoints.UserSubscription(userId), null, options, cancellationToken);
    }

    private static async Task<ApiOutcome<ContractCoopStatusResponse>> CoopStatus(
        IEggIncApi api,
        EndpointSpec endpoint,
        string contractId,
        string coopId,
        string? userId,
        TimeProvider? timeProvider,
        ApiCallOptions? options,
        CancellationToken cancellationToken) {
        try {
            ArgumentNullException.ThrowIfNull(api);
            ArgumentNullException.ThrowIfNull(contractId);
            ArgumentNullException.ThrowIfNull(coopId);
            var request = new ContractCoopStatusRequest {
                ContractIdentifier = contractId,
                CoopIdentifier = coopId.ToLowerInvariant(),
                ClientTimestamp = (timeProvider ?? TimeProvider.System).GetUtcNow().ToUnixTimeSeconds()
            };
            if (userId is not null) {
                request.UserId = userId;
            }
            request.ClientVersion = (options?.Versions ?? api.Versions).ClientVersion;
            var outcome = await api.Call<ContractCoopStatusRequest, ContractCoopStatusResponse>(
                endpoint,
                request,
                WithUser(options, userId),
                cancellationToken);
            return !outcome.Success
                ? ApiOutcome.Fail<ContractCoopStatusResponse>(outcome.Error ?? Errors.NoResponse)
                : string.Equals(outcome.Response?.CoopIdentifier, request.CoopIdentifier, StringComparison.OrdinalIgnoreCase)
                    ? outcome
                    : ApiOutcome.Fail<ContractCoopStatusResponse>(Errors.NoResponse);
        } catch (ArgumentNullException e) {
            return ApiOutcome.Fail<ContractCoopStatusResponse>(Fill(Errors.BotException, e.Message));
        }
    }

    private static EggIncFirstContactRequest FirstContactRequest(IEggIncApi api, string eiUserId, Platform? platform, ApiCallOptions? options) {
        var defaults = api.Preset.Rinfo;
        var request = new EggIncFirstContactRequest {
            EiUserId = eiUserId,
            DeviceId = defaults.DeviceId ?? eiUserId,
            ClientVersion = (options?.Versions ?? api.Versions).ClientVersion,
            Platform = platform ?? PresetPlatform(api)
        };
        if (defaults.FirstContactUsername) {
            request.Username = "";
        }
        return request;
    }

    private static Platform PresetPlatform(IEggIncApi api) {
        return Enum.Parse<Platform>(api.Preset.Rinfo.FirstContactPlatform, true);
    }

    private static string HttpOrError<T>(ApiOutcome<T> outcome) {
        return outcome.StatusCode is not null ? Errors.ErrorResponse : outcome.Error ?? Errors.ErrorResponse;
    }

    private static ApiCallOptions? WithUser(ApiCallOptions? options, string? userId) {
        return userId is null || options?.UserId is not null ? options : (options ?? new ApiCallOptions()) with { UserId = userId };
    }

    private static string Fill(string template, string value) {
        return template.Replace("{0}", value, StringComparison.Ordinal);
    }
}

internal static partial class EiApiLog {
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "bot_first_contact failed for {UserId} ({Error}) and no salt is configured for first_contact_secure fallback")]
    public static partial void BotFirstContactNoSalt(this ILogger logger, string userId, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "bot_first_contact did not return a backup ({Error}); falling back to first_contact_secure")]
    public static partial void BotFirstContactFallback(this ILogger logger, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "first_contact_secure also failed for {UserId} ({Error})")]
    public static partial void FirstContactSecureFailed(this ILogger logger, string userId, string error);
}

using System.Collections.ObjectModel;
using System.Net;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace EggSharp.Api;

public static class ApiPresets {
    public const string AndroidProfile = "Android";
    public const string IosProfile = "Ios";
    public const string CoopStatusProfile = "CoopStatus";
    public const string DefaultProfile = "Default";

    public static readonly ApiErrorText DefaultErrors = new(
        "HTTP Error: {0}",
        "No response",
        "Bot Exception: {0}",
        "Error response from API",
        "bot_first_contact returned no backup",
        "API salt not configured for {0}",
        "first_contact failed");

    private static readonly TimeSpan Egg9000Timeout = TimeSpan.FromSeconds(30);

    public static readonly ApiPreset Egg9000 = new(
        "Egg9000",
        [
            Egg9000Profile(AndroidProfile, "Dalvik/2.1.0 (Linux; U; Android 9; SM-G960U1 Build/PPR1.180610.011)", HttpVersion.Version11, null, new Dictionary<string, string> {
                ["Accept-Encoding"] = "gzip",
                ["Connection"] = "Keep-Alive"
            }),
            Egg9000Profile(IosProfile, "egginc/1.26.1.3 CFNetwork/1335.0.3 Darwin/21.6.0", HttpVersion.Version11, null, new Dictionary<string, string> {
                ["Accept-Encoding"] = "gzip, deflate, br",
                ["Connection"] = "Keep-Alive"
            }),
            Egg9000Profile(CoopStatusProfile, "egginc/1.35.3.1 CFNetwork/1410.1 Darwin/22.6.0", HttpVersion.Version20, "session=9cd692e4-050e-4cb9-a305-993bd28441b2", new Dictionary<string, string> {
                ["accept-encoding"] = "gzip, deflate, br",
                ["accept-language"] = "en-US,en;q=0.9",
                ["accept"] = "*/*"
            })
        ],
        new RinfoDefaults("IOS", "US", "en", false, null, "DROID", true),
        Table(IosProfile, AndroidProfile),
        Egg9000Retry,
        DefaultErrors);

    public static readonly ApiPreset EggLedger = new(
        "EggLedger",
        [new HeaderProfile(DefaultProfile, null, ReadOnlyDictionary<string, string>.Empty, HttpVersion.Version11, null, TimeSpan.FromSeconds(5), false, null)],
        new RinfoDefaults("IOS", null, null, null, "EggLedger", "IOS", false),
        Table(DefaultProfile, DefaultProfile),
        null,
        DefaultErrors);

    private static HeaderProfile Egg9000Profile(string name, string userAgent, Version httpVersion, string? cookie, Dictionary<string, string> headers) =>
        new(name, userAgent, headers, httpVersion, cookie, Egg9000Timeout, true, TimeSpan.FromMinutes(2));

    private static ResiliencePipelineBuilder Egg9000Retry(ResiliencePipelineBuilder builder) => builder
        .AddRetry(new RetryStrategyOptions {
            MaxRetryAttempts = 2,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(r => r is HttpResponseMessage { StatusCode: var status } && ((int)status >= 500 || status == HttpStatusCode.TooManyRequests))
        })
        .AddTimeout(Egg9000Timeout);

    private static Dictionary<(string Request, string? Response), EndpointSpec> Table(string ios, string android) => new() {
        [("ei.JoinCoopRequest", null)] = new("ei/join_coop", ios, RinfoMode.WithUser, false, false),
        [("ei.GetPeriodicalsRequest", null)] = new("ei/get_periodicals", ios, RinfoMode.WithoutUser, false, true),
        [("ei.ContractsInfoRequest", null)] = new("ei_ctx/get_contracts_info", ios, RinfoMode.WithUser, true, true),
        [("ei.CreateCoopRequest", null)] = new("ei/create_coop", ios, RinfoMode.WithUser, false, false),
        [("ei.UpdateCoopPermissionsRequest", null)] = new("ei/update_coop_permissions", ios, RinfoMode.WithUser, false, false),
        [("ei.ContractCoopStatusUpdateRequest", null)] = new("ei/update_coop_status_secure", ios, RinfoMode.WithUser, true, false),
        [("ei.ConfigRequest", null)] = new("ei/get_config", ios, RinfoMode.WithUser, false, false),
        [("ei.KickPlayerCoopRequest", null)] = new("ei/kick_player_coop", android, RinfoMode.None, false, false),
        [("ei.BasicRequestInfo", "ei.ContractPlayerInfo")] = new("ei_ctx/get_contract_player_info", ios, RinfoMode.WithUser, true, true),
        [("ei.BasicRequestInfo", "ei.MyContracts")] = new("ei_ctx/get_contracts_archive", ios, RinfoMode.WithUser, false, true),
        [("ei.MissionRequest", "ei.CompleteMissionResponse")] = new("ei_afx/complete_mission", ios, RinfoMode.None, false, true)
    };
}

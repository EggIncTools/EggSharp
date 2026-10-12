using EggSharp.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EggSharp.Api;

public static class EggIncApiServiceCollectionExtensions {
    public static IServiceCollection AddEggSharpApi(this IServiceCollection services, ApiPreset preset, Func<IServiceProvider, IEiCodec> codec, Func<IServiceProvider, AuxbrainSigner> signer, Func<IServiceProvider, VersionTripleSource> versions, Func<IServiceProvider, AuxbrainHosts>? hosts = null) {
        ArgumentNullException.ThrowIfNull(preset);
        foreach (var profile in preset.Profiles) {
            var client = services.AddHttpClient(EggIncApiHttp.ClientName(profile.Name), client => EggIncApiHttp.Configure(client, profile));
            if (profile.AutomaticDecompression || profile.PooledConnectionLifetime is not null)
                client.ConfigurePrimaryHttpMessageHandler(() => EggIncApiHttp.CreateHandler(profile));
        }
        services.AddSingleton<IEggIncApi>(sp => new EggIncApi(
            sp.GetRequiredService<IHttpClientFactory>(),
            new EggIncApiContext(preset, codec(sp), signer(sp), versions(sp), hosts?.Invoke(sp) ?? AuxbrainHosts.Live),
            EggIncApi.BuildPipeline(preset, sp.GetService<TimeProvider>() ?? TimeProvider.System),
            sp.GetService<IEggIncApiMetrics>(),
            sp.GetRequiredService<ILogger<EggIncApi>>()));
        return services;
    }
}

using System.Net;

namespace EggSharp.Api;

internal static class EggIncApiHttp {
    public static string ClientName(string profile) => "EggSharp.Api." + profile;

    public static void Configure(HttpClient client, HeaderProfile profile) {
        client.Timeout = profile.Timeout;
        client.DefaultRequestVersion = profile.HttpVersion;
        var headers = client.DefaultRequestHeaders;
        if (profile.Cookie is not null)
            headers.Add("Cookie", profile.Cookie);
        if (profile.UserAgent is not null)
            headers.Add("User-Agent", profile.UserAgent);
        foreach (var (name, value) in profile.Headers)
            headers.Add(name, value);
    }

    public static SocketsHttpHandler CreateHandler(HeaderProfile profile) => new() {
        AutomaticDecompression = profile.AutomaticDecompression ? DecompressionMethods.GZip | DecompressionMethods.Deflate : DecompressionMethods.None,
        PooledConnectionLifetime = profile.PooledConnectionLifetime ?? Timeout.InfiniteTimeSpan
    };
}

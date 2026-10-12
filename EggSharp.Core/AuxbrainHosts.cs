namespace EggSharp.Core;

public sealed class AuxbrainHosts(string defaultOrigin, IReadOnlyDictionary<string, string>? pathOrigins = null, Uri? baseAddress = null) {
    public static readonly AuxbrainHosts Live = new("https://www.auxbrain.com");

    private readonly KeyValuePair<string, string>[] _byLongestPrefix =
        [.. (pathOrigins ?? new Dictionary<string, string>()).OrderByDescending(p => p.Key.Length)];

    public Uri Resolve(string path) {
        var trimmed = path.TrimStart('/');
        var origin = defaultOrigin;
        foreach (var (prefix, candidate) in _byLongestPrefix) {
            if (trimmed.StartsWith(prefix.TrimStart('/'), StringComparison.Ordinal)) {
                origin = candidate;
                break;
            }
        }
        var uri = new Uri(origin.TrimEnd('/') + "/" + trimmed, UriKind.RelativeOrAbsolute);
        return uri.IsAbsoluteUri || baseAddress is null ? uri : new Uri(baseAddress, uri);
    }
}

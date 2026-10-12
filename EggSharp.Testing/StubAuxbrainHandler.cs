using System.Collections.Concurrent;
using System.Net;

namespace EggSharp.Testing;

public sealed record RecordedRequest(HttpMethod Method, string Path, string Query, byte[] Body, IReadOnlyDictionary<string, string> Headers);

public sealed class StubAuxbrainHandler(IReadOnlyDictionary<string, byte[]>? responses = null) : HttpMessageHandler {
    private readonly ConcurrentDictionary<string, byte[]> _responses = new(
        (responses ?? new Dictionary<string, byte[]>()).Select(kv => KeyValuePair.Create(Normalize(kv.Key), kv.Value)),
        StringComparer.Ordinal);

    private readonly ConcurrentQueue<RecordedRequest> _requests = new();

    public HttpStatusCode MissingStatus { get; init; } = HttpStatusCode.NotFound;

    public IReadOnlyList<RecordedRequest> Requests => [.. _requests];

    public StubAuxbrainHandler Serve(string path, byte[] body) {
        _responses[Normalize(path)] = body;
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        var path = Normalize(request.RequestUri?.AbsolutePath ?? "");
        var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        _requests.Enqueue(new RecordedRequest(request.Method, path, request.RequestUri?.Query ?? "", body, Headers(request)));
        return Match(path) is { } bytes
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), RequestMessage = request }
            : new HttpResponseMessage(MissingStatus) { RequestMessage = request };
    }

    private byte[]? Match(string path) {
        if (_responses.TryGetValue(path, out var exact))
            return exact;
        return _responses.FirstOrDefault(kv => path.EndsWith("/" + kv.Key, StringComparison.Ordinal)).Value;
    }

    private static Dictionary<string, string> Headers(HttpRequestMessage request) {
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> all = request.Content is null ? request.Headers : request.Headers.Concat(request.Content.Headers);
        return all.ToDictionary(h => h.Key, h => string.Join(", ", h.Value), StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Trim('/');
}

namespace EggSharp.Core;

public sealed record ApiResult<T>(T? Value, string? Error) {
    public bool Success => Error is null;

    public static implicit operator ApiResult<T>(T value) {
        return new(value, null);
    }
}

public static class ApiResult {
    public static ApiResult<T> Fail<T>(string error) => new(default, error);
}

public sealed record ApiOutcome<T>(T? Response, bool Success, string? Error, int? StatusCode = null, string? ResponseBody = null);

public static class ApiOutcome {
    public static ApiOutcome<T> Ok<T>(T response) => new(response, true, null);

    public static ApiOutcome<T> Fail<T>(string error, T? response = default) => new(response, false, error);
}

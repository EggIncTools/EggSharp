namespace EggSharp.Core.Tests;

public sealed class ApiResultTests {
    [Fact]
    public void ImplicitValue_IsSuccess() {
        ApiResult<string> result = "backup";
        Assert.True(result.Success);
        Assert.Equal("backup", result.Value);
    }

    [Fact]
    public void Fail_CarriesErrorAndIsNotSuccess() {
        var result = ApiResult.Fail<string>("No response");
        Assert.False(result.Success);
        Assert.Equal("No response", result.Error);
    }

    [Fact]
    public void OutcomeFail_KeepsRawResponse() {
        var outcome = ApiOutcome.Fail("bot_first_contact returned no backup", "raw");
        Assert.False(outcome.Success);
        Assert.Equal("raw", outcome.Response);
    }
}

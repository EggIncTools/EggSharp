namespace EggSharp.Core.Tests;

public sealed class AuxbrainSignerTests {
    private static readonly byte[] Message = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    [Fact]
    public void Sign_MatchesGoldenVector() {
        var signer = new AuxbrainSigner("eggsharp-golden");
        Assert.Equal("4961a98766cce3013f9074cfcbecbda431bcaed5d2b8d306f9392bdae3c00d28", signer.Sign(Message));
    }

    [Fact]
    public void Sign_DoesNotMutateCallerBytes() {
        var message = (byte[])Message.Clone();
        new AuxbrainSigner("eggsharp-golden").Sign(message);
        Assert.Equal(Message, message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoPhrase_CannotSignAndReturnsNull(string? phrase) {
        var signer = new AuxbrainSigner(phrase);
        Assert.False(signer.CanSign);
        Assert.Null(signer.Sign(Message));
    }

    [Fact]
    public void Sign_EmptyMessage_ReturnsHexCode() {
        var code = new AuxbrainSigner("eggsharp-golden").Sign([]);
        Assert.NotNull(code);
        Assert.Matches("^[0-9a-f]{64}$", code);
    }
}

namespace EggSharp.Core.Tests;

public sealed class EidTests {
    [Fact]
    public void TryParse_AcceptsEiAndSixteenDigits() {
        Assert.True(Eid.TryParse("EI1234567890123456", out var eid));
        Assert.Equal("EI1234567890123456", eid.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ei1234567890123456")]
    [InlineData(" EI1234567890123456")]
    [InlineData("EI1234567890123456 ")]
    [InlineData("EI123456789012345")]
    [InlineData("EI12345678901234567")]
    [InlineData("EI12345678901234X6")]
    [InlineData("EI１２３４５６７８９０１２３４５６")]
    public void TryParse_RejectsMalformed(string? text) {
        Assert.False(Eid.TryParse(text, out _));
    }

    [Fact]
    public void Parse_ThrowsFormatExceptionOnMalformed() {
        Assert.Throws<FormatException>(() => Eid.Parse("EI123"));
    }
}

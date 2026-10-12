using System.Text;

namespace EggSharp.Core.Tests;

public sealed class ProtobufUtf8SanitizerTests {
    [Fact]
    public void Sanitize_ScrubsInvalidBytesInNestedStringAndKeepsLengths() {
        byte[] name = [(byte)'a', 0xff, (byte)'b'];
        var inner = Field(1, name);
        var outer = Field(2, inner);
        var clean = ProtobufUtf8Sanitizer.Sanitize(outer);
        Assert.Equal(outer.Length, clean.Length);
        Assert.Equal(Field(2, Field(1, Encoding.ASCII.GetBytes("a?b"))), clean);
    }

    [Fact]
    public void Sanitize_LeavesValidUtf8Alone() {
        var message = Field(1, Encoding.UTF8.GetBytes("Égg"));
        Assert.Equal(message, ProtobufUtf8Sanitizer.Sanitize(message));
    }

    [Fact]
    public void IsInvalidUtf8_FindsInnerException() {
        var e = new InvalidOperationException("outer", new InvalidDataException("String is invalid UTF-8."));
        Assert.True(ProtobufUtf8Sanitizer.IsInvalidUtf8(e));
        Assert.False(ProtobufUtf8Sanitizer.IsInvalidUtf8(new InvalidOperationException("other")));
    }

    private static byte[] Field(int number, byte[] payload) => [(byte)((number << 3) | 2), (byte)payload.Length, .. payload];
}

namespace EggSharp.Core.Tests;

public sealed class VersionTripleTests {
    [Fact]
    public void Set_ReplacesCurrent() {
        var source = new VersionTripleSource(new VersionTriple(75, "1.35.6", "1.35.6.3"));
        var next = new VersionTriple(76, "1.35.7", "1.35.7.1");
        source.Set(next);
        Assert.Same(next, source.Current);
    }

    [Fact]
    public void Set_RejectsNull() {
        var source = new VersionTripleSource(new VersionTriple(75, "1.35.6", "1.35.6.3"));
        Assert.Throws<ArgumentNullException>(() => source.Set(null!));
    }
}

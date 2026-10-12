namespace EggSharp.Core;

public sealed record VersionTriple(uint ClientVersion, string AppVersion, string AppBuild);

public sealed class VersionTripleSource(VersionTriple initial) {
    private VersionTriple _current = initial;

    public VersionTriple Current => Volatile.Read(ref _current);

    public void Set(VersionTriple triple) {
        ArgumentNullException.ThrowIfNull(triple);
        Volatile.Write(ref _current, triple);
    }
}

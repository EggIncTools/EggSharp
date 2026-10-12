using System.Security.Cryptography;
using System.Text;

namespace EggSharp.Core;

public sealed class AuxbrainSigner(string? phrase) {
    private const uint Magic = 0x3b9af419;

    private readonly byte[]? _salt = string.IsNullOrEmpty(phrase)
        ? null
        : Encoding.ASCII.GetBytes(Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(phrase))));

    public bool CanSign => _salt is not null;

    public string? Sign(ReadOnlySpan<byte> message) {
        if (_salt is null)
            return null;
        var buffer = new byte[message.Length + _salt.Length];
        message.CopyTo(buffer);
        if (message.Length > 0)
            buffer[Magic % (uint)message.Length] = 0x1b;
        _salt.CopyTo(buffer, message.Length);
        return Convert.ToHexStringLower(SHA256.HashData(buffer));
    }
}

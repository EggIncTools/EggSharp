using System.IO.Compression;

namespace EggSharp.Core;

public static class AuxbrainFraming {
    public const int MaxInflatedBytes = 64 * 1024 * 1024;

    public static byte[] FromBase64Loose(string text) {
        var trimmed = text.Replace(' ', '+').Trim();
        var pad = trimmed.Length % 4;
        return Convert.FromBase64String(pad == 0 ? trimmed : trimmed.PadRight(trimmed.Length + 4 - pad, '='));
    }

    public static string FormBody(ReadOnlySpan<byte> payload) => "data=" + Uri.EscapeDataString(Convert.ToBase64String(payload));

    public static byte[] Decompress(byte[] compressed) {
        if (compressed is [0x1f, 0x8b, ..])
            return Inflate(compressed, s => new GZipStream(s, CompressionMode.Decompress));
        return TryInflate(compressed, s => new ZLibStream(s, CompressionMode.Decompress))
            ?? TryInflate(compressed, s => new DeflateStream(s, CompressionMode.Decompress))
            ?? compressed;
    }

    private static byte[] Inflate(byte[] compressed, Func<Stream, Stream> wrap) {
        using var input = new MemoryStream(compressed);
        using var decompressor = wrap(input);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = decompressor.Read(buffer)) > 0) {
            total += read;
            if (total > MaxInflatedBytes)
                throw new PayloadTooLargeException(MaxInflatedBytes);
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static byte[]? TryInflate(byte[] compressed, Func<Stream, Stream> wrap) {
        try {
            return Inflate(compressed, wrap);
        } catch (InvalidDataException) {
            return null;
        }
    }
}

public sealed class PayloadTooLargeException(long limit) : IOException($"Decompressed payload exceeds {limit} bytes.");

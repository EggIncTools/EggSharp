namespace EggSharp.Core;

public static class ProtobufUtf8Sanitizer {
    public static bool IsInvalidUtf8(Exception e) {
        for (var current = e; current is not null; current = current.InnerException) {
            if (current is System.Text.DecoderFallbackException || current.Message.Contains("invalid UTF-8", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static byte[] Sanitize(byte[] data) {
        if (data.Length == 0)
            return data;
        var copy = (byte[])data.Clone();
        SanitizeRange(copy, 0, copy.Length);
        return copy;
    }

    private static void SanitizeRange(byte[] buffer, int start, int end) {
        var pos = start;
        while (pos < end) {
            if (!TryReadVarint(buffer, ref pos, end, out var tag))
                return;
            switch ((int)(tag & 0x7)) {
                case 0:
                    if (!TryReadVarint(buffer, ref pos, end, out _))
                        return;
                    break;
                case 1:
                    pos += 8;
                    if (pos > end)
                        return;
                    break;
                case 5:
                    pos += 4;
                    if (pos > end)
                        return;
                    break;
                case 2:
                    if (!TryReadVarint(buffer, ref pos, end, out var len))
                        return;
                    var fieldEnd = pos + (long)len;
                    if (fieldEnd > end)
                        return;
                    if (LooksLikeMessage(buffer, pos, (int)fieldEnd))
                        SanitizeRange(buffer, pos, (int)fieldEnd);
                    else
                        ScrubInvalidUtf8(buffer, pos, (int)fieldEnd);
                    pos = (int)fieldEnd;
                    break;
                default:
                    return;
            }
        }
    }

    private static bool LooksLikeMessage(byte[] buffer, int start, int end) {
        if (start >= end)
            return false;
        var pos = start;
        var fields = 0;
        while (pos < end) {
            if (!TryReadVarint(buffer, ref pos, end, out var tag) || tag >> 3 == 0)
                return false;
            switch ((int)(tag & 0x7)) {
                case 0:
                    if (!TryReadVarint(buffer, ref pos, end, out _))
                        return false;
                    break;
                case 1:
                    pos += 8;
                    break;
                case 5:
                    pos += 4;
                    break;
                case 2:
                    if (!TryReadVarint(buffer, ref pos, end, out var len) || pos + (long)len > end)
                        return false;
                    pos += (int)len;
                    break;
                default:
                    return false;
            }
            if (pos > end)
                return false;
            fields++;
        }
        return fields > 0;
    }

    private static void ScrubInvalidUtf8(byte[] buffer, int start, int end) {
        var i = start;
        while (i < end) {
            var b = buffer[i];
            if (b < 0x80) {
                i++;
                continue;
            }
            var seqLen = b switch {
                >= 0xC2 and <= 0xDF => 2,
                >= 0xE0 and <= 0xEF => 3,
                >= 0xF0 and <= 0xF4 => 4,
                _ => 0
            };
            if (seqLen == 0 || i + seqLen > end || !ValidContinuation(buffer, i, seqLen)) {
                buffer[i] = (byte)'?';
                i++;
                continue;
            }
            i += seqLen;
        }
    }

    private static bool ValidContinuation(byte[] buffer, int start, int seqLen) {
        for (var k = 1; k < seqLen; k++) {
            if (buffer[start + k] is < 0x80 or > 0xBF)
                return false;
        }
        return true;
    }

    private static bool TryReadVarint(byte[] buffer, ref int pos, int end, out ulong value) {
        value = 0;
        for (var shift = 0; pos < end && shift < 64; shift += 7) {
            var b = buffer[pos++];
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
                return true;
        }
        return false;
    }
}

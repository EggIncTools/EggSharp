namespace EggSharp.ProtoParity;

internal static class ProtoTokenizer {
    public static List<string> Tokenize(string text) {
        var tokens = new List<string>();
        var i = 0;
        while (i < text.Length) {
            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (char.IsWhiteSpace(c)) {
                i++;
            } else if (c == '/' && next == '/') {
                var newline = text.IndexOf('\n', i);
                i = newline < 0 ? text.Length : newline + 1;
            } else if (c == '/' && next == '*') {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? text.Length : close + 2;
            } else if (c is '"' or '\'') {
                var start = i++;
                while (i < text.Length && text[i] != c)
                    i += text[i] == '\\' ? 2 : 1;
                i = Math.Min(i + 1, text.Length);
                tokens.Add(text[start..i]);
            } else if (IsWordStart(c) || (c == '-' && char.IsAsciiDigit(next))) {
                var start = i++;
                while (i < text.Length && IsWordStart(text[i]))
                    i++;
                tokens.Add(text[start..i]);
            } else {
                tokens.Add(c.ToString());
                i++;
            }
        }
        return tokens;
    }

    private static bool IsWordStart(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '.';
}

internal sealed class TokenReader(List<string> tokens) {
    private int position;

    public bool AtEnd => position >= tokens.Count;

    public string Next() => position < tokens.Count ? tokens[position++] : throw new FormatException("Unexpected end of proto.");

    public bool TryConsume(string token) {
        if (position >= tokens.Count || tokens[position] != token)
            return false;
        position++;
        return true;
    }

    public void Expect(string token) {
        var actual = Next();
        if (actual != token)
            throw new FormatException($"Expected '{token}' but found '{actual}'.");
    }

    public void SkipStatement() {
        var depth = 0;
        string token;
        do {
            token = Next();
            if (token == "{")
                depth++;
            else if (token == "}")
                depth--;
        } while (token != ";" || depth > 0);
    }

    public void SkipBlock() {
        var token = Next();
        while (token != "{")
            token = Next();
        SkipBracketed("{", "}");
    }

    public void SkipBracketed(string open, string close) {
        var depth = 1;
        while (depth > 0) {
            var token = Next();
            if (token == open)
                depth++;
            else if (token == close)
                depth--;
        }
    }
}

using System.Text;

namespace EggSharp.ProtoParity;

public static class CSharpNames {
    public static string Property(string fieldName, string messageName) {
        var name = PascalCase(fieldName);
        return name == messageName ? name + "_" : name;
    }

    public static string PascalCase(string snake) {
        var result = new StringBuilder(snake.Length);
        var capitalizeNext = true;
        foreach (var c in snake) {
            if (char.IsAsciiLetterLower(c)) {
                result.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            } else if (char.IsAsciiLetterUpper(c)) {
                result.Append(c);
                capitalizeNext = false;
            } else if (char.IsAsciiDigit(c)) {
                result.Append(c);
                capitalizeNext = true;
            } else {
                capitalizeNext = true;
            }
        }
        return result.ToString();
    }

    public static string EnumValue(string enumName, string valueName) {
        var result = ShoutyToPascalCase(RemovePrefix(enumName, valueName));
        return result.Length == 0 || char.IsAsciiDigit(result[0]) ? "_" + result : result;
    }

    private static string RemovePrefix(string prefix, string value) {
        var normalized = string.Concat(prefix.Where(c => c != '_').Select(char.ToLowerInvariant));
        var prefixIndex = 0;
        var valueIndex = 0;
        while (prefixIndex < normalized.Length && valueIndex < value.Length) {
            if (value[valueIndex] == '_') {
                valueIndex++;
                continue;
            }
            if (char.ToLowerInvariant(value[valueIndex]) != normalized[prefixIndex])
                return value;
            valueIndex++;
            prefixIndex++;
        }
        if (prefixIndex < normalized.Length)
            return value;
        while (valueIndex < value.Length && value[valueIndex] == '_')
            valueIndex++;
        return valueIndex == value.Length ? value : value[valueIndex..];
    }

    private static string ShoutyToPascalCase(string input) {
        var result = new StringBuilder(input.Length);
        var previous = '_';
        foreach (var current in input) {
            if (!char.IsAsciiLetterOrDigit(current)) {
                previous = current;
                continue;
            }
            if (!char.IsAsciiLetterOrDigit(previous) || char.IsAsciiDigit(previous))
                result.Append(char.ToUpperInvariant(current));
            else if (char.IsAsciiLetterLower(previous))
                result.Append(current);
            else
                result.Append(char.ToLowerInvariant(current));
            previous = current;
        }
        return result.ToString();
    }
}

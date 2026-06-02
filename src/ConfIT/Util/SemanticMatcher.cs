using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace ConfIT.Util;

public static class SemanticMatcher
{
    private static readonly IReadOnlyDictionary<string, SemanticMatcherFunc> BuiltIns =
        new Dictionary<string, SemanticMatcherFunc>
        {
            ["isUuid"] = (v, _) => CheckIsUuid(v),
            ["isIsoDate"] = (v, _) => CheckIsIsoDate(v),
            ["isIsoDateTime"] = (v, _) => CheckIsIsoDateTime(v),
            ["isEmail"] = (v, _) => CheckIsEmail(v),
            ["isNull"] = (v, _) => CheckIsNull(v),
            ["isNotNull"] = (v, _) => CheckIsNotNull(v),
            ["isEmpty"] = (v, _) => CheckIsEmpty(v),
            ["isNotEmpty"] = (v, _) => CheckIsNotEmpty(v),
            ["greaterThan"] = CheckGreaterThan,
            ["lessThan"] = CheckLessThan,
            ["hasLength"] = CheckHasLength
        };

    public static void ValidateSpecs(
        Dictionary<string, string>? semantic,
        IReadOnlyDictionary<string, SemanticMatcherFunc>? customMatchers)
    {
        if (semantic is not { Count: > 0 }) return;

        if (customMatchers != null)
            foreach (var key in customMatchers.Keys)
                if (BuiltIns.ContainsKey(key))
                    throw new ArgumentException(
                        $"Custom matcher '{key}' conflicts with a built-in matcher name");

        foreach (var (fieldPath, spec) in semantic)
        {
            var (name, _) = ParseSpec(spec, fieldPath);
            if (!BuiltIns.ContainsKey(name) && (customMatchers == null || !customMatchers.ContainsKey(name)))
                throw new ArgumentException(
                    $"Unknown semantic matcher '{name}' for field '{fieldPath}'");
        }
    }

    public static void Apply(
        JToken actual,
        JToken expected,
        Dictionary<string, string>? semantic,
        IReadOnlyDictionary<string, SemanticMatcherFunc>? customMatchers)
    {
        if (semantic is not { Count: > 0 }) return;

        foreach (var (fieldPath, matcherSpec) in semantic)
        {
            var (name, param) = ParseSpec(matcherSpec, fieldPath);

            if (!BuiltIns.TryGetValue(name, out var fn))
                if (customMatchers == null || !customMatchers.TryGetValue(name, out fn!))
                    throw new ArgumentException(
                        $"Unknown semantic matcher '{name}' for field '{fieldPath}'");

            var jPath = fieldPath.Replace("__", ".");
            var actualField = actual.SelectToken(jPath)
                              ?? throw new InvalidOperationException(
                                  $"Semantic matcher '{matcherSpec}': field '{fieldPath}' was absent from the response");

            var result = fn(actualField, param);
            if (result != null)
                false.Should().BeTrue($"field '{fieldPath}' [{matcherSpec}]: {result}");

            actualField.Parent?.Remove();
            expected.SelectToken(jPath)?.Parent?.Remove();
        }
    }

    private static (string name, string? param) ParseSpec(string spec, string fieldPath)
    {
        var parenIdx = spec.IndexOf('(');
        if (parenIdx == -1)
            return (spec, null);

        if (!spec.EndsWith(')'))
            throw new ArgumentException(
                $"Malformed matcher spec '{spec}' for field '{fieldPath}': missing closing parenthesis");

        return (spec[..parenIdx], spec[(parenIdx + 1)..^1]);
    }

    private static string? CheckIsUuid(JToken value)
    {
        if (value.Type != JTokenType.String)
            return $"expected a string but got {value.Type}";
        var s = value.Value<string>()!;
        return Regex.IsMatch(s, @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
            ? null
            : $"expected UUID format (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx) but got '{s}'";
    }

    private static string? CheckIsIsoDate(JToken value)
    {
        // Newtonsoft auto-parses datetime-looking strings to JTokenType.Date
        if (value.Type == JTokenType.Date)
        {
            var dt = value.Value<DateTime>();
            return dt.TimeOfDay == TimeSpan.Zero
                ? null
                : "expected date-only (yyyy-MM-dd) but the value has a time component";
        }

        if (value.Type != JTokenType.String)
            return $"expected a string but got {value.Type}";
        var s = value.Value<string>()!;
        return DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : $"expected ISO date (yyyy-MM-dd) but got '{s}'";
    }

    private static string? CheckIsIsoDateTime(JToken value)
    {
        // Newtonsoft auto-parses datetime strings to JTokenType.Date — already valid
        if (value.Type == JTokenType.Date) return null;
        if (value.Type != JTokenType.String)
            return $"expected a string but got {value.Type}";
        var s = value.Value<string>()!;
        if (!s.Contains('T'))
            return $"expected ISO datetime (yyyy-MM-ddTHH:mm:ss...) but got '{s}'";
        return DateTimeOffset.TryParse(s, null, DateTimeStyles.RoundtripKind, out _)
            ? null
            : $"expected ISO datetime but got '{s}'";
    }

    private static string? CheckIsEmail(JToken value)
    {
        if (value.Type != JTokenType.String)
            return $"expected a string but got {value.Type}";
        var s = value.Value<string>()!;
        return Regex.IsMatch(s, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")
            ? null
            : $"expected email format but got '{s}'";
    }

    private static string? CheckIsNull(JToken value)
    {
        return value.Type == JTokenType.Null ? null : $"expected null but got '{value}'";
    }

    private static string? CheckIsNotNull(JToken value)
    {
        return value.Type != JTokenType.Null ? null : "expected non-null value but got null";
    }

    private static string? CheckIsEmpty(JToken value)
    {
        return value switch
        {
            { Type: JTokenType.String } when value.Value<string>()?.Length == 0 => null,
            { Type: JTokenType.String } s => $"expected empty string but got '{s.Value<string>()}'",
            JArray { Count: 0 } => null,
            JArray arr => $"expected empty array but got array with {arr.Count} element(s)",
            JObject obj when !obj.HasValues => null,
            JObject => "expected empty object but got non-empty object",
            _ => $"isEmpty requires string, array, or object but got {value.Type}"
        };
    }

    private static string? CheckIsNotEmpty(JToken value)
    {
        return value switch
        {
            { Type: JTokenType.String } when value.Value<string>()?.Length > 0 => null,
            { Type: JTokenType.String } => "expected non-empty string but got empty string",
            JArray { Count: > 0 } => null,
            JArray => "expected non-empty array but got empty array",
            JObject obj when obj.HasValues => null,
            JObject => "expected non-empty object but got empty object",
            _ => $"isNotEmpty requires string, array, or object but got {value.Type}"
        };
    }

    private static string? CheckGreaterThan(JToken value, string? param)
    {
        return CheckNumeric(value, param, "greaterThan", (actual, threshold) => actual > threshold, ">");
    }

    private static string? CheckLessThan(JToken value, string? param)
    {
        return CheckNumeric(value, param, "lessThan", (actual, threshold) => actual < threshold, "<");
    }

    private static string? CheckNumeric(
        JToken value, string? param, string matcherName,
        Func<decimal, decimal, bool> predicate, string symbol)
    {
        if (value.Type is not JTokenType.Integer and not JTokenType.Float)
            return $"{matcherName} requires a numeric field but got {value.Type}";
        if (!decimal.TryParse(param, NumberStyles.Number, CultureInfo.InvariantCulture, out var threshold))
            return $"{matcherName} parameter '{param}' is not a valid number";
        var actual = value.Value<decimal>();
        return predicate(actual, threshold) ? null : $"expected value {symbol} {threshold} but got {actual}";
    }

    private static string? CheckHasLength(JToken value, string? param)
    {
        if (param is null) return "hasLength requires a length parameter";

        var (actualLength, typeDesc) = value switch
        {
            { Type: JTokenType.String } => (value.Value<string>()?.Length ?? 0, "string"),
            JArray arr => (arr.Count, "array"),
            JObject obj => (obj.Count, "object"),
            _ => (-1, null)
        };

        if (typeDesc is null)
            return $"hasLength requires string, array, or object but got {value.Type}";

        var parts = param.Split(',');

        if (parts.Length == 1)
        {
            if (!int.TryParse(parts[0].Trim(), out var expected))
                return $"hasLength parameter '{param}' is not a valid integer";
            return actualLength == expected
                ? null
                : $"expected {typeDesc} of length {expected} but got {actualLength}";
        }

        if (parts.Length == 2
            && int.TryParse(parts[0].Trim(), out var min)
            && int.TryParse(parts[1].Trim(), out var max))
            return actualLength >= min && actualLength <= max
                ? null
                : $"expected {typeDesc} length in [{min},{max}] (inclusive) but got {actualLength}";

        return $"hasLength parameter '{param}' must be 'n' or 'min,max'";
    }
}
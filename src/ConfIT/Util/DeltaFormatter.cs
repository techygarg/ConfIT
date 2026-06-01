namespace ConfIT.Util;

public static class DeltaFormatter
{
    public static string Format(JToken delta)
    {
        var lines = new List<string>();

        // JsonDiffPatch returns a root-level JArray when the entire value was replaced
        // (e.g. Diff({}, null) → [{}, null]). Walk only handles JObject deltas.
        if (delta is JArray rootArr)
            AppendChange(lines, "(root)", rootArr);
        else
            Walk(delta, string.Empty, lines);

        return lines.Count == 0
            ? string.Empty
            : "Response body mismatch:\n\n" + string.Join("\n\n", lines);
    }

    private static void Walk(JToken node, string path, List<string> lines)
    {
        if (node is not JObject obj) return;

        var isArrayContext = obj["_t"]?.ToString() == "a";

        foreach (var prop in obj.Properties())
        {
            if (prop.Name == "_t") continue;

            var childPath = BuildPath(path, prop.Name, isArrayContext);

            if (prop.Value is JArray arr)
                AppendChange(lines, childPath, arr);
            else if (prop.Value is JObject)
                Walk(prop.Value, childPath, lines);
        }
    }

    private static string BuildPath(string parent, string key, bool isArrayContext)
    {
        if (isArrayContext)
        {
            var index = key.TrimStart('_');
            return string.IsNullOrEmpty(parent) ? $"[{index}]" : $"{parent}[{index}]";
        }
        return string.IsNullOrEmpty(parent) ? key : $"{parent}.{key}";
    }

    private static void AppendChange(List<string> lines, string path, JArray arr)
    {
        string expected, actual;

        switch (arr.Count)
        {
            case 2: // modification: Diff(actual, expected) → [actualVal, expectedVal]
                actual   = FormatValue(arr[0]);
                expected = FormatValue(arr[1]);
                break;
            case 1: // field in expected, not in actual → [expectedVal]
                expected = FormatValue(arr[0]);
                actual   = "<missing>";
                break;
            case 3: // field in actual, not in expected → [actualVal, 0, 0]
                actual   = FormatValue(arr[0]);
                expected = "<absent>";
                break;
            default:
                return;
        }

        lines.Add($"{path}\n  expected: {expected}\n  actual:   {actual}");
    }

    private static string FormatValue(JToken value) => value.Type switch
    {
        JTokenType.String  => $"\"{value}\"",
        JTokenType.Date    => $"\"{((JValue)value).Value<DateTime>():s}\"",
        JTokenType.Null    => "null",
        JTokenType.Boolean => value.Value<bool>().ToString().ToLower(),
        _                  => value.ToString()
    };
}

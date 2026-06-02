using System.IO;

namespace ConfIT.Config;

internal static class Validate
{
    internal static void Required(string? value, string fieldPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException(
                $"'{fieldPath}' is required but was not set in {filePath}");
    }

    internal static void OneOf(string? value, string fieldPath, string filePath, params string[] allowed)
    {
        if (!allowed.Contains(value))
            throw new InvalidDataException(
                $"'{fieldPath}' must be one of [{string.Join(", ", allowed)}] in {filePath}. Got: '{value}'");
    }

    internal static void ExactlyOneSet(string sectionPath, string filePath,
        params (string Name, JToken? Value)[] fields)
    {
        var set = fields
            .Where(f => f.Value is not null && f.Value.Type != JTokenType.Null)
            .ToList();
        if (set.Count == 1) return;
        var names = string.Join(", ", fields.Select(f => f.Name));
        var detail = set.Count == 0
            ? "None were set."
            : $"Multiple were set: {string.Join(", ", set.Select(f => f.Name))}.";
        throw new InvalidDataException(
            $"Exactly one of [{names}] must be set in '{sectionPath}' in {filePath}. {detail}");
    }

    internal static void KnownKeys(JObject obj, IReadOnlySet<string> known, string sectionPath, string filePath)
    {
        foreach (var key in obj.Properties().Select(p => p.Name))
            if (!known.Contains(key))
                throw new InvalidDataException(
                    $"Unknown key '{key}' in '{sectionPath}' in {filePath}. " +
                    $"Known keys: [{string.Join(", ", known.OrderBy(k => k))}]");
    }
}
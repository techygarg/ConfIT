using System.IO;

namespace ConfIT.Config;

internal static class Validate
{
    internal static void Required(string? value, string fieldPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"'{fieldPath}' is required but was not set in {filePath}");
    }

    internal static void OneOf(string? value, string fieldPath, string filePath, params string[] allowed)
    {
        if (!allowed.Contains(value))
            throw new InvalidDataException(
                $"'{fieldPath}' must be one of [{string.Join(", ", allowed)}] in {filePath}. Got: '{value}'");
    }

    internal static void ExactlyOneSet(string sectionPath, string filePath, params (string Name, object? Value)[] fields)
    {
        var set = fields.Where(f => f.Value is not null).ToList();
        if (set.Count == 1) return;

        var names  = string.Join(", ", fields.Select(f => f.Name));
        var detail = set.Count == 0
            ? "None were set."
            : $"Multiple were set: {string.Join(", ", set.Select(f => f.Name))}.";

        throw new InvalidDataException(
            $"Exactly one of [{names}] must be set in '{sectionPath}' in {filePath}. {detail}");
    }
}

using System.IO;

namespace ConfIT.Util;

internal static class DependencyValidator
{
    internal static void Validate(IReadOnlyList<(string Name, JToken Token)> tests, string filePath)
    {
        var indexByName = tests
            .Select((t, i) => (t.Name, Index: i))
            .ToDictionary(x => x.Name, x => x.Index);

        for (var i = 0; i < tests.Count; i++)
        {
            var (name, token) = tests[i];
            var depends = token["depends"];
            if (depends is null) continue;

            foreach (var dep in depends.Values<string>())
            {
                if (!indexByName.ContainsKey(dep))
                    throw new InvalidDataException(
                        $"Test '{name}' in '{filePath}' declares 'depends: [{dep}]' " +
                        $"but no test named '{dep}' exists in this file.");

                if (indexByName[dep] >= i)
                    throw new InvalidDataException(
                        $"Test '{name}' in '{filePath}' declares 'depends: [{dep}]' " +
                        $"but '{dep}' is defined after it. " +
                        "Dependencies must reference tests defined earlier in the file.");
            }
        }
    }
}

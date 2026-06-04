using System.IO;
using YamlDotNet.Core;

namespace ConfIT.Util;

public static class TestReader
{
    public static IEnumerable<object[]> GetTestsForAFile(string testFolderName, string fileName)
    {
        var filePath = Path.GetFullPath($"{testFolderName}/{fileName}");
        return GetTestsFromParsedFile(filePath);
    }

    public static IEnumerable<object[]> GetTestsForAFolder(string testFolderName)
    {
        // Sort alphabetically — Directory.GetFiles returns inode order on Linux,
        // which is non-deterministic. Tests that depend on prior state (e.g. create
        // then retrieve) must run in consistent file order across all platforms.
        var files = Directory.GetFiles(Path.GetFullPath(testFolderName))
            .Where(f => Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase)
                        || IsYaml(f))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        return files.SelectMany(GetTestsFromParsedFile);
    }

    private static IEnumerable<object[]> GetTestsFromParsedFile(string filePath)
    {
        var content     = File.ReadAllText(filePath);
        var fileContent = IsYaml(filePath) ? LoadYaml(content, filePath) : JObject.Parse(content);
        var fileName    = Path.GetFileName(filePath);

        var tests = fileContent.Properties()
            .Select(p => (Name: p.Name, Token: p.Value))
            .ToList();

        DependencyValidator.Validate(tests, filePath);

        foreach (var (name, token) in tests)
            yield return [name, token, fileName];
    }

    private static JObject LoadYaml(string content, string filePath)
    {
        try
        {
            return YamlConverter.ToJObject(content);
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException($"YAML parse error in '{filePath}': {ex.Message}", ex);
        }
    }

    private static bool IsYaml(string path)
    {
        return Path.GetExtension(path).Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               Path.GetExtension(path).Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }
}

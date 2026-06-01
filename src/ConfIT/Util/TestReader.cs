using System.IO;
using YamlDotNet.Core;

namespace ConfIT.Util;

public static class TestReader
{
    public static IEnumerable<object[]> GetTestsForAFile(string testFolderName, string fileName)
    {
        var filePath    = Path.GetFullPath($"{testFolderName}/{fileName}");
        var content     = File.ReadAllText(filePath);
        var fileContent = IsYaml(filePath) ? LoadYaml(content, filePath) : JObject.Parse(content);

        foreach (var scenario in fileContent.Properties())
            yield return [scenario.Name, scenario.Value, fileName];
    }

    public static IEnumerable<object[]> GetTestsForAFolder(string testFolderName)
    {
        var files = Directory.GetFiles(Path.GetFullPath(testFolderName))
            .Where(f => Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase)
                     || IsYaml(f));

        foreach (var filePath in files)
        {
            var content     = File.ReadAllText(filePath);
            var fileContent = IsYaml(filePath) ? LoadYaml(content, filePath) : JObject.Parse(content);
            var fileName    = Path.GetFileName(filePath);
            foreach (var scenario in fileContent.Properties())
                yield return [scenario.Name, scenario.Value, fileName];
        }
    }

    private static JObject LoadYaml(string content, string filePath)
    {
        try   { return YamlConverter.ToJObject(content); }
        catch (YamlException ex)
        { throw new InvalidDataException($"YAML parse error in '{filePath}': {ex.Message}", ex); }
    }

    private static bool IsYaml(string path) =>
        Path.GetExtension(path).Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".yml",  StringComparison.OrdinalIgnoreCase);
}

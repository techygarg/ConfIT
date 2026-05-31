using System.IO;

namespace ConfIT.Util;

public static class TestReader
{
    public static IEnumerable<object[]> GetTestsForAFile(string testFolderName, string fileName)
    {
        var fileContent = JObject.Parse(File.ReadAllText(Path.GetFullPath($"{testFolderName}/{fileName}")));

        foreach (var scenario in fileContent.Properties())
            yield return [scenario.Name, scenario.Value];
    }

    public static IEnumerable<object[]> GetTestsForAFolder(string testFolderName)
    {
        var files = Directory.GetFiles(Path.GetFullPath(testFolderName))
            .Where(f => Path.GetExtension(f).Equals(".json", StringComparison.OrdinalIgnoreCase));

        foreach (var filePath in files)
        {
            var fileContent = JObject.Parse(File.ReadAllText(filePath));
            foreach (var scenario in fileContent.Properties())
                yield return [scenario.Name, scenario.Value];
        }
    }
}

using System.IO;

namespace ConfIT.Extension;

public static class StringExtensions
{
    public static JToken ReadJsonResponse(this string testName, string responseFolderPath)
    {
        return JToken.Parse(File.ReadAllText($"{responseFolderPath}/{testName.ToLower()}.json"));
    }
}
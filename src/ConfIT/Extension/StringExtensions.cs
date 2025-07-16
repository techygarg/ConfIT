using System.IO;
using Newtonsoft.Json.Linq;

namespace ConfIT.Extension
{
    public static class StringExtensions
    {
        public static JToken ReadJsonResponse(this string testName, string responseFolderPath) =>
            JToken.Parse(File.ReadAllText($"{responseFolderPath}/{testName.ToLower()}.json"));

        public static bool IsNullOrWhiteSpace(this string input) =>
            string.IsNullOrWhiteSpace(input);
    }
}
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ConfIT.Util
{
    public static class TestReader
    {
        public static IEnumerable<object[]> GetTestsForAFile(string testFolderName, string fileName)
        {
            var fileContent = JObject.Parse(File.ReadAllText(Path.GetFullPath($"{testFolderName}/" + fileName)));
            if (fileContent == null) yield break;

            foreach (var testCaseScenario in fileContent.Properties())
            {
                if (testCaseScenario == null) continue;
                yield return new object[]
                {
                    testCaseScenario.Name,
                    testCaseScenario.Value
                };
            }
        }

        public static IEnumerable<object[]> GetTestsForAFolder(string testFolderName)
        {
            var result = new List<object[]>();
            var filesPath = Directory.GetFiles(Path.GetFullPath(testFolderName));

            foreach (var filePath in filesPath)
            {
                var fileContent = JObject.Parse(File.ReadAllText(filePath));
                if (fileContent == null) continue;

                result.AddRange(fileContent.Properties().Select(testCaseScenario =>
                    new object[]
                    {
                        testCaseScenario.Name,
                        testCaseScenario.Value
                    }).ToList());
            }

            return result;
        }
    }
}
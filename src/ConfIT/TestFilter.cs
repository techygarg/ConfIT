using System.Collections.Generic;
using System.Linq;
using ConfIT.Extension;
using static System.Environment;

namespace ConfIT
{
    public class TestFilter
    {
        public List<string> Tags { get; set; }
        public List<string> TestNames { get; set; }

        public static TestFilter CreateForTags(string tags)
        {
            return new TestFilter
            {
                Tags = tags?.Split(",").ToList() ?? new List<string>(),
            };
        }

        public static TestFilter CreateForTagsFromEnvVariable(string tagKey)
        {
            List<string> tags = null;

            if (!tagKey.IsNullOrWhiteSpace())
                tags = GetEnvironmentVariable(tagKey)?.Split(",").ToList() ?? new List<string>();

            return new TestFilter
            {
                Tags = tags ?? new List<string>()
            };
        }

        public static TestFilter CreateForTests(string testNames)
        {
            return new TestFilter
            {
                TestNames = testNames?.Split(",").ToList() ?? new List<string>(),
            };
        }
        
        public static TestFilter CreateForTestsFromEnvVariable(string testNamesKey)
        {
            List<string> tests = null;

            if (!testNamesKey.IsNullOrWhiteSpace())
                tests = GetEnvironmentVariable(testNamesKey)?.Split(",").ToList() ?? new List<string>();

            return new TestFilter
            {
                TestNames = tests ?? new List<string>()
            };
        }
    }
}
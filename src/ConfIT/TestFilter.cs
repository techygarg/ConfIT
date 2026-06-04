using static System.Environment;

namespace ConfIT;

public class TestFilter
{
    public List<string> Tags { get; set; }
    public List<string> TestNames { get; set; }

    public static TestFilter CreateForTags(string tags)
    {
        return new TestFilter
        {
            Tags = tags?.Split(',').ToList() ?? []
        };
    }

    public static TestFilter CreateForTagsFromEnvVariable(string tagKey)
    {
        return new TestFilter
        {
            Tags = string.IsNullOrWhiteSpace(tagKey)
                ? []
                : GetEnvironmentVariable(tagKey)?.Split(',').ToList() ?? []
        };
    }

    public static TestFilter CreateForTests(string testNames)
    {
        return new TestFilter
        {
            TestNames = testNames?.Split(',').ToList() ?? []
        };
    }

    public static TestFilter CreateForTestsFromEnvVariable(string testNamesKey)
    {
        return new TestFilter
        {
            TestNames = string.IsNullOrWhiteSpace(testNamesKey)
                ? []
                : GetEnvironmentVariable(testNamesKey)?.Split(',').ToList() ?? []
        };
    }
}
using static System.Environment;

namespace ConfIT;

public class TestFilter
{
    public List<string> Tags { get; set; }
    public List<string> TestNames { get; set; }

    public static TestFilter CreateForTags(string tags) => new()
    {
        Tags = tags?.Split(',').ToList() ?? []
    };

    public static TestFilter CreateForTagsFromEnvVariable(string tagKey) => new()
    {
        Tags = string.IsNullOrWhiteSpace(tagKey) ? []
            : GetEnvironmentVariable(tagKey)?.Split(',').ToList() ?? []
    };

    public static TestFilter CreateForTests(string testNames) => new()
    {
        TestNames = testNames?.Split(',').ToList() ?? []
    };

    public static TestFilter CreateForTestsFromEnvVariable(string testNamesKey) => new()
    {
        TestNames = string.IsNullOrWhiteSpace(testNamesKey) ? []
            : GetEnvironmentVariable(testNamesKey)?.Split(',').ToList() ?? []
    };
}

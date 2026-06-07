using static System.Environment;

namespace ConfIT;

public class TestFilter
{
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<string> TestNames { get; init; } = [];

    public static TestFilter CreateForTags(string tags) => new()
    {
        Tags = tags?.Split(',').ToList() ?? []
    };

    public static TestFilter CreateForTagsFromEnvVariable(string tagKey) => new()
    {
        Tags = string.IsNullOrWhiteSpace(tagKey)
            ? []
            : GetEnvironmentVariable(tagKey)?.Split(',').ToList() ?? []
    };

    public static TestFilter CreateForTests(string testNames) => new()
    {
        TestNames = testNames?.Split(',').ToList() ?? []
    };

    public static TestFilter CreateForTestsFromEnvVariable(string testNamesKey) => new()
    {
        TestNames = string.IsNullOrWhiteSpace(testNamesKey)
            ? []
            : GetEnvironmentVariable(testNamesKey)?.Split(',').ToList() ?? []
    };
}

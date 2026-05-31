namespace ConfIT.UnitTest;

public class TestFilterTests
{
    [Fact]
    public void CreateForTags_WithValidTags_ReturnsFilterWithTags()
    {
        // Given
        var tags = "tag1,tag2,tag3";

        // When
        var filter = TestFilter.CreateForTags(tags);

        // Then
        filter.Tags.Should().BeEquivalentTo(["tag1", "tag2", "tag3"]);
    }

    [Fact]
    public void CreateForTags_WithNullTags_ReturnsEmptyTagsList()
    {
        // When
        var filter = TestFilter.CreateForTags(null);

        // Then
        filter.Tags.Should().BeEmpty();
    }

    [Fact]
    public void CreateForTags_WithEmptyString_ReturnsFilterWithSingleEmptyTag()
    {
        // When
        var filter = TestFilter.CreateForTags("");

        // Then
        filter.Tags.Should().ContainSingle(string.Empty);
    }

    [Fact]
    public void CreateForTagsFromEnvVariable_WithExistingVariable_ReturnsFilterWithTags()
    {
        // Given
        const string Key = "TEST_TAGS";
        Environment.SetEnvironmentVariable(Key, "tag1,tag2,tag3");
        try
        {
            // When
            var filter = TestFilter.CreateForTagsFromEnvVariable(Key);

            // Then
            filter.Tags.Should().BeEquivalentTo(["tag1", "tag2", "tag3"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Key, null);
        }
    }

    [Fact]
    public void CreateForTagsFromEnvVariable_WithNonExistentVariable_ReturnsEmptyTagsList()
    {
        // When
        var filter = TestFilter.CreateForTagsFromEnvVariable("NON_EXISTENT_TAG_KEY");

        // Then
        filter.Tags.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateForTagsFromEnvVariable_WithNullOrEmptyKey_ReturnsEmptyTagsList(string key)
    {
        // When
        var filter = TestFilter.CreateForTagsFromEnvVariable(key);

        // Then
        filter.Tags.Should().BeEmpty();
    }

    [Fact]
    public void CreateForTests_WithValidTestNames_ReturnsFilterWithTestNames()
    {
        // Given
        var testNames = "test1,test2,test3";

        // When
        var filter = TestFilter.CreateForTests(testNames);

        // Then
        filter.TestNames.Should().BeEquivalentTo(["test1", "test2", "test3"]);
    }

    [Fact]
    public void CreateForTests_WithNullTestNames_ReturnsEmptyTestNamesList()
    {
        // When
        var filter = TestFilter.CreateForTests(null);

        // Then
        filter.TestNames.Should().BeEmpty();
    }

    [Fact]
    public void CreateForTestsFromEnvVariable_WithExistingVariable_ReturnsFilterWithTestNames()
    {
        // Given
        const string Key = "TEST_NAMES";
        Environment.SetEnvironmentVariable(Key, "test1,test2,test3");
        try
        {
            // When
            var filter = TestFilter.CreateForTestsFromEnvVariable(Key);

            // Then
            filter.TestNames.Should().BeEquivalentTo(["test1", "test2", "test3"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Key, null);
        }
    }

    [Fact]
    public void CreateForTestsFromEnvVariable_WithNonExistentVariable_ReturnsEmptyTestNamesList()
    {
        // When
        var filter = TestFilter.CreateForTestsFromEnvVariable("NON_EXISTENT_TEST_NAMES_KEY");

        // Then
        filter.TestNames.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateForTestsFromEnvVariable_WithNullOrEmptyKey_ReturnsEmptyTestNamesList(string key)
    {
        // When
        var filter = TestFilter.CreateForTestsFromEnvVariable(key);

        // Then
        filter.TestNames.Should().BeEmpty();
    }

    [Fact]
    public void CreateForTags_DoesNotSetTestNames()
    {
        // When
        var filter = TestFilter.CreateForTags("tag1");

        // Then
        filter.TestNames.Should().BeNull();
    }

    [Fact]
    public void CreateForTests_DoesNotSetTags()
    {
        // When
        var filter = TestFilter.CreateForTests("test1");

        // Then
        filter.Tags.Should().BeNull();
    }
}

using FluentAssertions;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ConfIT.UnitTest
{
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
            filter.Tags.Should().NotBeNull();
            filter.Tags.Should().HaveCount(3);
            filter.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2", "tag3" });
        }

        [Fact]
        public void CreateForTags_WithNullTags_ReturnsEmptyTagsList()
        {
            // When
            var filter = TestFilter.CreateForTags(null);

            // Then
            filter.Tags.Should().NotBeNull();
            filter.Tags.Should().BeEmpty();
        }

        [Fact]
        public void CreateForTags_WithEmptyString_ReturnsFilterWithSingleEmptyTag()
        {
            // Given
            var tags = "";

            // When
            var filter = TestFilter.CreateForTags(tags);

            // Then
            filter.Tags.Should().NotBeNull();
            filter.Tags.Should().HaveCount(1);
            filter.Tags.Should().ContainSingle(string.Empty);
        }

        [Fact]
        public void CreateForTagsFromEnvVariable_WithExistingVariable_ReturnsFilterWithTags()
        {
            // Given
            var tagKey = "TEST_TAGS";
            Environment.SetEnvironmentVariable(tagKey, "tag1,tag2,tag3");

            // When
            var filter = TestFilter.CreateForTagsFromEnvVariable(tagKey);

            // Then
            filter.Tags.Should().NotBeNull();
            filter.Tags.Should().HaveCount(3);
            filter.Tags.Should().BeEquivalentTo(new[] { "tag1", "tag2", "tag3" });

            // Cleanup
            Environment.SetEnvironmentVariable(tagKey, null);
        }

        [Fact]
        public void CreateForTagsFromEnvVariable_WithNonExistentVariable_ReturnsEmptyTagsList()
        {
            // Given
            var tagKey = "NON_EXISTENT_TAG_KEY";

            // When
            var filter = TestFilter.CreateForTagsFromEnvVariable(tagKey);

            // Then
            filter.Tags.Should().NotBeNull();
            filter.Tags.Should().BeEmpty();
        }

        [Fact]
        public void CreateForTagsFromEnvVariable_WithNullOrEmptyKey_ReturnsEmptyTagsList()
        {
            // When
            var filterWithNull = TestFilter.CreateForTagsFromEnvVariable(null);
            var filterWithEmpty = TestFilter.CreateForTagsFromEnvVariable(string.Empty);

            // Then
            filterWithNull.Tags.Should().NotBeNull();
            filterWithNull.Tags.Should().BeEmpty();
            filterWithEmpty.Tags.Should().NotBeNull();
            filterWithEmpty.Tags.Should().BeEmpty();
        }

        [Fact]
        public void CreateForTests_WithValidTestNames_ReturnsFilterWithTestNames()
        {
            // Given
            var testNames = "test1,test2,test3";

            // When
            var filter = TestFilter.CreateForTests(testNames);

            // Then
            filter.TestNames.Should().NotBeNull();
            filter.TestNames.Should().HaveCount(3);
            filter.TestNames.Should().BeEquivalentTo(new[] { "test1", "test2", "test3" });
        }

        [Fact]
        public void CreateForTests_WithNullTestNames_ReturnsEmptyTestNamesList()
        {
            // When
            var filter = TestFilter.CreateForTests(null);

            // Then
            filter.TestNames.Should().NotBeNull();
            filter.TestNames.Should().BeEmpty();
        }

        [Fact]
        public void CreateForTestsFromEnvVariable_WithExistingVariable_ReturnsFilterWithTestNames()
        {
            // Given
            var testNamesKey = "TEST_NAMES";
            Environment.SetEnvironmentVariable(testNamesKey, "test1,test2,test3");

            // When
            var filter = TestFilter.CreateForTestsFromEnvVariable(testNamesKey);

            // Then
            filter.TestNames.Should().NotBeNull();
            filter.TestNames.Should().HaveCount(3);
            filter.TestNames.Should().BeEquivalentTo(new[] { "test1", "test2", "test3" });

            // Cleanup
            Environment.SetEnvironmentVariable(testNamesKey, null);
        }

        [Fact]
        public void CreateForTestsFromEnvVariable_WithNonExistentVariable_ReturnsEmptyTestNamesList()
        {
            // Given
            var testNamesKey = "NON_EXISTENT_TEST_NAMES_KEY";

            // When
            var filter = TestFilter.CreateForTestsFromEnvVariable(testNamesKey);

            // Then
            filter.TestNames.Should().NotBeNull();
            filter.TestNames.Should().BeEmpty();
        }

        [Fact]
        public void CreateForTestsFromEnvVariable_WithNullOrEmptyKey_ReturnsEmptyTestNamesList()
        {
            // When
            var filterWithNull = TestFilter.CreateForTestsFromEnvVariable(null);
            var filterWithEmpty = TestFilter.CreateForTestsFromEnvVariable(string.Empty);

            // Then
            filterWithNull.TestNames.Should().NotBeNull();
            filterWithNull.TestNames.Should().BeEmpty();
            filterWithEmpty.TestNames.Should().NotBeNull();
            filterWithEmpty.TestNames.Should().BeEmpty();
        }
    }
} 
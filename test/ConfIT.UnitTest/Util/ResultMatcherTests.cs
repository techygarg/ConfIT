using ConfIT.Server.Dto;
using ConfIT.Util;
using Xunit.Sdk;

namespace ConfIT.UnitTest.Util
{
    public class ResultMatcherTests
    {
        [Fact]
        public void MatchResponseBody_ShouldPassForIdenticalJson()
        {
            // Given
            var actual = JToken.Parse("{'name': 'test', 'value': 123}");
            var expected = JToken.Parse("{'name': 'test', 'value': 123}");
            var matcher = new Matcher();

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().NotThrow();
        }

        [Fact]
        public void MatchResponseBody_ShouldMatchWhenPatternMatchesFieldValue()
        {
            // Given
            var actual = JToken.Parse("{'id': 'abc-123', 'name': 'test'}");
            var expected = JToken.Parse("{'name': 'test'}");
            var pattern = "[a-z]+-\\d+";
            var matcher = new Matcher
            {
                Pattern = new Dictionary<string, string> { { "id", pattern } }
            };

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().NotThrow();
        }

        [Fact]
        public void MatchResponseBody_ShouldIgnoreSpecifiedFields()
        {
            // Given
            var actual = JToken.Parse("{'id': '123', 'name': 'test', 'timestamp': '2023-01-01'}");
            var expected = JToken.Parse("{'name': 'test'}");
            var matcher = new Matcher
            {
                Ignore = new List<string> { "id", "timestamp" }
            };

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().NotThrow();
        }

        [Fact]
        public void MatchResponseBody_ShouldHandleNestedJsonStructures()
        {
            // Given
            var actual = JToken.Parse("{'user': {'id': '123', 'details': {'age': 30}}}");
            var expected = JToken.Parse("{'user': {'details': {'age': 30}}}");
            var matcher = new Matcher
            {
                Ignore = new List<string> { "user__id" }
            };

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().NotThrow();
        }

        [Fact]
        public void MatchResponseBody_ShouldRemoveFieldsWithSpecificParentPath()
        {
            // Given
            var actual = JToken.Parse("{'data': {'id': '123'}, 'metadata': {'id': '456'}}");
            var expected = JToken.Parse("{'data': {}, 'metadata': {'id': '456'}}");
            var matcher = new Matcher
            {
                Ignore = new List<string> { "data__id" }
            };

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().NotThrow();
        }

        [Fact]
        public void MatchResponseBody_ShouldHandleMultipleParentLevels()
        {
            // Given
            var actual = JToken.Parse("{'level1': {'level2': {'level3': {'id': '123'}}}}");
            var expected = JToken.Parse("{'level1': {'level2': {'level3': {}}}}");
            var matcher = new Matcher
            {
                Ignore = new List<string> { "level1__level2__level3__id" }
            };

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().NotThrow();
        }

        [Fact]
        public void MatchResponseBody_ShouldHandleNullActualResponse()
        {
            // Given
            JToken actual = null;
            var expected = JToken.Parse("{}");
            var matcher = new Matcher();

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().Throw<NullReferenceException>();
        }

        [Fact]
        public void MatchResponseBody_ShouldHandleNullExpectedResponse()
        {
            // Given
            var actual = JToken.Parse("{}");
            JToken expected = null;
            var matcher = new Matcher();

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().Throw<XunitException>();
        }

        [Fact]
        public void MatchResponseBody_ShouldHandleNullMatcher()
        {
            // Given
            var actual = JToken.Parse("{'id': '123', 'name': 'test'}");
            var expected = JToken.Parse("{'id': '123', 'name': 'test'}");
            Matcher matcher = null;

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().NotThrow();
        }

        [Fact]
        public void MatchResponseBody_ShouldHandleEmptyPatternDictionary()
        {
            // Given
            var actual = JToken.Parse("{'id': '123', 'name': 'test'}");
            var expected = JToken.Parse("{'id': '123', 'name': 'test'}");
            var matcher = new Matcher
            {
                Pattern = new Dictionary<string, string>()
            };

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().NotThrow();
        }

        [Fact]
        public void MatchResponseBody_ShouldHandleEmptyIgnoreList()
        {
            // Given
            var actual = JToken.Parse("{'id': '123', 'name': 'test'}");
            var expected = JToken.Parse("{'id': '123', 'name': 'test'}");
            var matcher = new Matcher
            {
                Ignore = new List<string>()
            };

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().NotThrow();
        }

        [Fact]
        public void MatchResponseBody_ShouldHandleInvalidRegexPattern()
        {
            // Given
            var actual = JToken.Parse("{'id': '123', 'name': 'test'}");
            var expected = JToken.Parse("{'name': 'test'}");
            var matcher = new Matcher
            {
                Pattern = new Dictionary<string, string> { { "id", "[" } }
            };

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void MatchResponseBody_ShouldHandleAdditionalFieldsInExpected()
        {
            // Given
            var actual = JToken.Parse("{'name': 'John', 'age': 30}");
            var expected = JToken.Parse("{'name': 'John', 'age': 30, 'extra': 'field'}");
            var matcher = new Matcher();

            // When
            var matchAction = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

            // Then
            matchAction.Should().Throw<Xunit.Sdk.XunitException>()
                .WithMessage("*"); // The exact message will depend on your implementation
        }
    }
}
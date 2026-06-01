namespace ConfIT.UnitTest.Util;

public class ResultMatcherTests
{
    [Fact]
    public void MatchResponseBody_ShouldPassForIdenticalJson()
    {
        // Given
        var actual   = JToken.Parse("{'name': 'test', 'value': 123}");
        var expected = JToken.Parse("{'name': 'test', 'value': 123}");
        var matcher  = new Matcher();

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_ShouldMatchWhenPatternMatchesFieldValue()
    {
        // Given
        var actual   = JToken.Parse("{'id': 'abc-123', 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Pattern = new Dictionary<string, string> { ["id"] = "[a-z]+-\\d+" } };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_ShouldIgnoreSpecifiedFields()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test', 'timestamp': '2023-01-01'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Ignore = ["id", "timestamp"] };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_ShouldHandleNestedJsonStructures()
    {
        // Given
        var actual   = JToken.Parse("{'user': {'id': '123', 'details': {'age': 30}}}");
        var expected = JToken.Parse("{'user': {'details': {'age': 30}}}");
        var matcher  = new Matcher { Ignore = ["user__id"] };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_ShouldRemoveFieldsWithSpecificParentPath()
    {
        // Given
        var actual   = JToken.Parse("{'data': {'id': '123'}, 'metadata': {'id': '456'}}");
        var expected = JToken.Parse("{'data': {}, 'metadata': {'id': '456'}}");
        var matcher  = new Matcher { Ignore = ["data__id"] };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_ShouldHandleMultipleParentLevels()
    {
        // Given
        var actual   = JToken.Parse("{'level1': {'level2': {'level3': {'id': '123'}}}}");
        var expected = JToken.Parse("{'level1': {'level2': {'level3': {}}}}");
        var matcher  = new Matcher { Ignore = ["level1__level2__level3__id"] };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_ShouldThrowOnNullActualResponse()
    {
        // Given
        JToken actual   = null;
        var   expected  = JToken.Parse("{}");
        var   matcher   = new Matcher();

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().Throw<Exception>();
    }

    [Fact]
    public void MatchResponseBody_ShouldThrowOnNullExpectedResponse()
    {
        // Given
        var   actual   = JToken.Parse("{}");
        JToken expected = null;
        var   matcher  = new Matcher();

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().Throw<Exception>();
    }

    [Fact]
    public void MatchResponseBody_ShouldHandleNullMatcher()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test'}");
        var expected = JToken.Parse("{'id': '123', 'name': 'test'}");

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_ShouldHandleEmptyPatternDictionary()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test'}");
        var expected = JToken.Parse("{'id': '123', 'name': 'test'}");
        var matcher  = new Matcher { Pattern = new Dictionary<string, string>() };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_ShouldHandleEmptyIgnoreList()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test'}");
        var expected = JToken.Parse("{'id': '123', 'name': 'test'}");
        var matcher  = new Matcher { Ignore = [] };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_ShouldThrowOnInvalidRegexPattern()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Pattern = new Dictionary<string, string> { ["id"] = "[" } };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MatchResponseBody_ShouldFailWhenExpectedHasFieldMissingFromActual()
    {
        // Given
        var actual   = JToken.Parse("{'name': 'John', 'age': 30}");
        var expected = JToken.Parse("{'name': 'John', 'age': 30, 'extra': 'field'}");
        var matcher  = new Matcher();

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().Throw<XunitException>();
    }

    // ── Field-level failure output ────────────────────────────────────────────

    [Fact]
    public void MatchResponseBody_WhenFieldsDiffer_FailureMessageNamesField()
    {
        // Given
        var actual   = JToken.Parse("{'name': 'alice'}");
        var expected = JToken.Parse("{'name': 'bob'}");

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, null);
        var ex = action.Should().Throw<Exception>().Which;

        // Then
        ex.Message.Should().Contain("name");
        ex.Message.Should().Contain("expected:");
        ex.Message.Should().Contain("\"bob\"");
        ex.Message.Should().Contain("actual:");
        ex.Message.Should().Contain("\"alice\"");
    }

    [Fact]
    public void MatchResponseBody_WhenMultipleFieldsDiffer_FailureMessageIncludesAllFields()
    {
        // Given
        var actual   = JToken.Parse("{'name': 'alice', 'age': 30}");
        var expected = JToken.Parse("{'name': 'bob',   'age': 31}");

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, null);
        var ex = action.Should().Throw<Exception>().Which;

        // Then
        ex.Message.Should().Contain("name");
        ex.Message.Should().Contain("age");
    }

    [Fact]
    public void MatchResponseBody_WhenBodyDiffers_FailureMessageContainsHeader()
    {
        // Given
        var actual   = JToken.Parse("{'x': 1}");
        var expected = JToken.Parse("{'x': 2}");

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, null);
        var ex = action.Should().Throw<Exception>().Which;

        // Then
        ex.Message.Should().Contain("Response body mismatch:");
    }

    // ── Semantic integration ───────────────────────────────────────────────────

    [Fact]
    public void MatchResponseBody_WithSemanticMatcher_ValidatesAndRemovesField()
    {
        // Given
        var actual   = JToken.Parse("{'id': 5, 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Semantic = new Dictionary<string, string> { ["id"] = "greaterThan(0)" } };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_WithFailingSemanticMatcher_ThrowsWithFieldName()
    {
        // Given
        var actual   = JToken.Parse("{'id': -1, 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Semantic = new Dictionary<string, string> { ["id"] = "greaterThan(0)" } };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().Throw<Exception>().WithMessage("*id*greaterThan(0)*");
    }

    [Fact]
    public void MatchResponseBody_WithCustomMatcher_AppliesItCorrectly()
    {
        // Given
        var actual   = JToken.Parse("{'code': 'DOM-42', 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Semantic = new Dictionary<string, string> { ["code"] = "isDomainId" } };
        var custom   = new Dictionary<string, SemanticMatcherFunc>
        {
            ["isDomainId"] = (token, _) =>
                token.Value<string>()?.StartsWith("DOM-") == true ? null : "Expected DOM-{n} format"
        };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher, custom);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_WithAllThreeMatcherTypes_AppliesEachCorrectly()
    {
        // Given
        var actual   = JToken.Parse("{'id': 'a1b2c3d4-e5f6-7890-abcd-ef1234567890', 'code': 'ABC-123', 'createdAt': '2026-05-31', 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher
        {
            Semantic = new Dictionary<string, string> { ["id"] = "isUuid" },
            Pattern  = new Dictionary<string, string> { ["code"] = "[A-Z]+-\\d+" },
            Ignore   = ["createdAt"]
        };

        // When
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        action.Should().NotThrow();
    }
}

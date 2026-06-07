namespace ConfIT.UnitTest.Util;

public class ResultMatcherTests
{
    #region Basic matching

    [Fact]
    public void MatchResponseBody_IdenticalJson_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'name': 'test', 'value': 123}");
        var expected = JToken.Parse("{'name': 'test', 'value': 123}");

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, new Matcher());

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_PatternMatchesFieldValue_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'id': 'abc-123', 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Pattern = new Dictionary<string, string> { ["id"] = "[a-z]+-\\d+" } };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_IgnoreFields_ExcludesFromComparison()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test', 'timestamp': '2023-01-01'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Ignore = ["id", "timestamp"] };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_NestedIgnoreField_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'user': {'id': '123', 'details': {'age': 30}}}");
        var expected = JToken.Parse("{'user': {'details': {'age': 30}}}");
        var matcher  = new Matcher { Ignore = ["user__id"] };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_PathScopedIgnore_OnlyRemovesMatchingField()
    {
        // Given
        var actual   = JToken.Parse("{'data': {'id': '123'}, 'metadata': {'id': '456'}}");
        var expected = JToken.Parse("{'data': {}, 'metadata': {'id': '456'}}");
        var matcher  = new Matcher { Ignore = ["data__id"] };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_DeepNestedIgnore_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'level1': {'level2': {'level3': {'id': '123'}}}}");
        var expected = JToken.Parse("{'level1': {'level2': {'level3': {}}}}");
        var matcher  = new Matcher { Ignore = ["level1__level2__level3__id"] };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_NullMatcher_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test'}");
        var expected = JToken.Parse("{'id': '123', 'name': 'test'}");

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, null);

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_EmptyPatternDictionary_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test'}");
        var expected = JToken.Parse("{'id': '123', 'name': 'test'}");
        var matcher  = new Matcher { Pattern = new Dictionary<string, string>() };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_EmptyIgnoreList_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test'}");
        var expected = JToken.Parse("{'id': '123', 'name': 'test'}");
        var matcher  = new Matcher { Ignore = [] };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeTrue();
    }

    #endregion

    #region Error cases

    [Fact]
    public void MatchResponseBody_NullActual_ThrowsException()
    {
        // Given
        JToken actual   = null;
        var expected    = JToken.Parse("{}");

        // When / Then — null actual is a caller error; DeepClone throws
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, new Matcher());
        action.Should().Throw<Exception>();
    }

    [Fact]
    public void MatchResponseBody_NullExpected_ReturnsFailed()
    {
        // Given
        var actual      = JToken.Parse("{}");
        JToken expected = null;

        // When — null expected means "expect nothing"; diff against actual returns failure
        var result = ResultMatcher.MatchResponseBody(actual, expected, new Matcher());

        // Then
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public void MatchResponseBody_NullExpectedWithSemanticMatcher_DoesNotThrow()
    {
        // Given — response.body omitted in test definition but semantic matcher declared
        var actual  = JToken.Parse("{'id': 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'}");
        var matcher = new Matcher { Semantic = new Dictionary<string, string> { ["id"] = "isUuid" } };

        // When / Then — must not throw NullReferenceException
        var act = () => ResultMatcher.MatchResponseBody(actual, null, matcher);
        act.Should().NotThrow();
    }

    [Fact]
    public void MatchResponseBody_InvalidRegexPattern_ThrowsArgumentException()
    {
        // Given
        var actual   = JToken.Parse("{'id': '123', 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Pattern = new Dictionary<string, string> { ["id"] = "[" } };

        // When / Then
        var action = () => ResultMatcher.MatchResponseBody(actual, expected, matcher);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MatchResponseBody_ExpectedFieldMissingFromActual_ReturnsFailed()
    {
        // Given
        var actual   = JToken.Parse("{'name': 'John', 'age': 30}");
        var expected = JToken.Parse("{'name': 'John', 'age': 30, 'extra': 'field'}");

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, new Matcher());

        // Then
        result.Passed.Should().BeFalse();
        result.Description.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region Failure output

    [Fact]
    public void MatchResponseBody_FieldsDiffer_DescriptionNamesField()
    {
        // Given
        var actual   = JToken.Parse("{'name': 'alice'}");
        var expected = JToken.Parse("{'name': 'bob'}");

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, null);

        // Then
        result.Passed.Should().BeFalse();
        result.Description.Should().Contain("name")
            .And.Contain("expected:")
            .And.Contain("\"bob\"")
            .And.Contain("actual:")
            .And.Contain("\"alice\"");
    }

    [Fact]
    public void MatchResponseBody_MultipleFieldsDiffer_DescriptionIncludesAllFields()
    {
        // Given
        var actual   = JToken.Parse("{'name': 'alice', 'age': 30}");
        var expected = JToken.Parse("{'name': 'bob',   'age': 31}");

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, null);

        // Then
        result.Passed.Should().BeFalse();
        result.Description.Should().Contain("name").And.Contain("age");
    }

    [Fact]
    public void MatchResponseBody_BodyDiffers_DescriptionContainsHeader()
    {
        // Given
        var actual   = JToken.Parse("{'x': 1}");
        var expected = JToken.Parse("{'x': 2}");

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, null);

        // Then
        result.Passed.Should().BeFalse();
        result.Description.Should().Contain("Response body mismatch:");
    }

    #endregion

    #region Semantic matchers

    [Fact]
    public void MatchResponseBody_SemanticMatcherValid_PassesAndRemovesField()
    {
        // Given
        var actual   = JToken.Parse("{'id': 5, 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Semantic = new Dictionary<string, string> { ["id"] = "greaterThan(0)" } };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_SemanticMatcherFails_ReturnedDescriptionNamesField()
    {
        // Given
        var actual   = JToken.Parse("{'id': -1, 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher { Semantic = new Dictionary<string, string> { ["id"] = "greaterThan(0)" } };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeFalse();
        result.Description.Should().Contain("id").And.Contain("greaterThan(0)");
    }

    [Fact]
    public void MatchResponseBody_CustomMatcher_PassesForValidValue()
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
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher, custom);

        // Then
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void MatchResponseBody_AllThreeMatcherTypes_EachApplied()
    {
        // Given
        var actual = JToken.Parse(
            "{'id': 'a1b2c3d4-e5f6-7890-abcd-ef1234567890', 'code': 'ABC-123', 'createdAt': '2026-05-31', 'name': 'test'}");
        var expected = JToken.Parse("{'name': 'test'}");
        var matcher  = new Matcher
        {
            Semantic = new Dictionary<string, string> { ["id"]   = "isUuid" },
            Pattern  = new Dictionary<string, string> { ["code"] = "[A-Z]+-\\d+" },
            Ignore   = ["createdAt"]
        };

        // When
        var result = ResultMatcher.MatchResponseBody(actual, expected, matcher);

        // Then
        result.Passed.Should().BeTrue();
    }

    #endregion
}

namespace ConfIT.UnitTest.Util;

public class SemanticMatcherTests
{
    private static string? Apply(JToken actual, JToken expected, string field, string spec,
        IReadOnlyDictionary<string, SemanticMatcherFunc>? custom = null)
        => SemanticMatcher.Apply(actual, expected, new Dictionary<string, string> { [field] = spec }, custom);

    #region Format matchers

    [Theory]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [InlineData("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    public void Apply_IsUuidWithValidUuid_Passes(string uuid)
    {
        var result = Apply(JToken.Parse($"{{'id': '{uuid}'}}"), JToken.Parse("{}"), "id", "isUuid");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsUuidWithInvalidValue_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'id': 'not-a-uuid'}"), JToken.Parse("{}"), "id", "isUuid");
        result.Should().NotBeNull().And.Contain("id").And.Contain("isUuid");
    }

    [Fact]
    public void Apply_IsIsoDateWithValidDate_Passes()
    {
        var result = Apply(JToken.Parse("{'date': '2026-05-31'}"), JToken.Parse("{}"), "date", "isIsoDate");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsIsoDateWithDateTime_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'date': '2026-05-31T10:30:00Z'}"), JToken.Parse("{}"), "date", "isIsoDate");
        result.Should().NotBeNull().And.Contain("date").And.Contain("isIsoDate");
    }

    [Theory]
    [InlineData("2026-05-31T10:30:00Z")]
    [InlineData("2026-05-31T10:30:00+05:30")]
    [InlineData("2026-05-31T10:30:00.123Z")]
    public void Apply_IsIsoDateTimeWithValidDateTime_Passes(string dateTime)
    {
        var result = Apply(JToken.Parse($"{{'createdAt': '{dateTime}'}}"), JToken.Parse("{}"), "createdAt", "isIsoDateTime");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsIsoDateTimeWithDateOnly_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'createdAt': '2026-05-31'}"), JToken.Parse("{}"), "createdAt", "isIsoDateTime");
        result.Should().NotBeNull().And.Contain("createdAt").And.Contain("isIsoDateTime");
    }

    [Fact]
    public void Apply_IsEmailWithValidEmail_Passes()
    {
        var result = Apply(JToken.Parse("{'email': 'user@example.com'}"), JToken.Parse("{}"), "email", "isEmail");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsEmailWithInvalidValue_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'email': 'not-an-email'}"), JToken.Parse("{}"), "email", "isEmail");
        result.Should().NotBeNull().And.Contain("email").And.Contain("isEmail");
    }

    #endregion

    #region Null matchers

    [Fact]
    public void Apply_IsNullWithNullValue_Passes()
    {
        var result = Apply(JToken.Parse("{'deletedAt': null}"), JToken.Parse("{}"), "deletedAt", "isNull");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsNullWithNonNullValue_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'deletedAt': '2026-05-31'}"), JToken.Parse("{}"), "deletedAt", "isNull");
        result.Should().NotBeNull().And.Contain("deletedAt").And.Contain("isNull");
    }

    [Fact]
    public void Apply_IsNotNullWithPresentValue_Passes()
    {
        var result = Apply(JToken.Parse("{'id': 1}"), JToken.Parse("{}"), "id", "isNotNull");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsNotNullWithNullValue_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'id': null}"), JToken.Parse("{}"), "id", "isNotNull");
        result.Should().NotBeNull().And.Contain("id").And.Contain("isNotNull");
    }

    #endregion

    #region Emptiness matchers

    [Fact]
    public void Apply_IsEmptyWithEmptyString_Passes()
    {
        var result = Apply(JToken.Parse("{'name': ''}"), JToken.Parse("{}"), "name", "isEmpty");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsEmptyWithEmptyArray_Passes()
    {
        var result = Apply(JToken.Parse("{'items': []}"), JToken.Parse("{}"), "items", "isEmpty");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsEmptyWithEmptyObject_Passes()
    {
        var result = Apply(JToken.Parse("{'meta': {}}"), JToken.Parse("{}"), "meta", "isEmpty");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsNotEmptyWithNonEmptyArray_Passes()
    {
        var result = Apply(JToken.Parse("{'items': [1, 2]}"), JToken.Parse("{}"), "items", "isNotEmpty");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_IsNotEmptyWithEmptyString_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'name': ''}"), JToken.Parse("{}"), "name", "isNotEmpty");
        result.Should().NotBeNull().And.Contain("name").And.Contain("isNotEmpty");
    }

    [Fact]
    public void Apply_IsNotEmptyWithEmptyArray_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'items': []}"), JToken.Parse("{}"), "items", "isNotEmpty");
        result.Should().NotBeNull().And.Contain("items").And.Contain("isNotEmpty");
    }

    #endregion

    #region Numeric matchers

    [Fact]
    public void Apply_GreaterThanWithValueAboveThreshold_Passes()
    {
        var result = Apply(JToken.Parse("{'count': 5}"), JToken.Parse("{}"), "count", "greaterThan(0)");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_GreaterThanWithValueAtThreshold_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'count': 0}"), JToken.Parse("{}"), "count", "greaterThan(0)");
        result.Should().NotBeNull().And.Contain("count").And.Contain("greaterThan(0)");
    }

    [Fact]
    public void Apply_GreaterThanWithNonNumericValue_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'score': 'high'}"), JToken.Parse("{}"), "score", "greaterThan(0)");
        result.Should().NotBeNull().And.Contain("score");
    }

    [Fact]
    public void Apply_LessThanWithValueBelowThreshold_Passes()
    {
        var result = Apply(JToken.Parse("{'age': 17}"), JToken.Parse("{}"), "age", "lessThan(18)");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_LessThanWithValueAtThreshold_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'age': 18}"), JToken.Parse("{}"), "age", "lessThan(18)");
        result.Should().NotBeNull().And.Contain("age").And.Contain("lessThan(18)");
    }

    #endregion

    #region Size matchers

    [Fact]
    public void Apply_HasLengthWithExactMatch_Passes()
    {
        var result = Apply(JToken.Parse("{'zip': '12345'}"), JToken.Parse("{}"), "zip", "hasLength(5)");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_HasLengthWithinInclusiveRange_Passes()
    {
        var result = Apply(JToken.Parse("{'name': 'Al'}"), JToken.Parse("{}"), "name", "hasLength(1,50)");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_HasLengthOnArray_Passes()
    {
        var result = Apply(JToken.Parse("{'tags': ['a', 'b', 'c']}"), JToken.Parse("{}"), "tags", "hasLength(3)");
        result.Should().BeNull();
    }

    [Fact]
    public void Apply_HasLengthWithMismatch_ReturnsFailure()
    {
        var result = Apply(JToken.Parse("{'zip': '1234'}"), JToken.Parse("{}"), "zip", "hasLength(5)");
        result.Should().NotBeNull().And.Contain("zip").And.Contain("hasLength(5)");
    }

    #endregion

    #region Nested fields

    [Fact]
    public void Apply_NestedFieldPath_PassesForValidValue()
    {
        var actual   = JToken.Parse("{'user': {'profile': {'id': 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'}}}");
        var expected = JToken.Parse("{'user': {'profile': {}}}");

        var result = Apply(actual, expected, "user__profile__id", "isUuid");
        result.Should().BeNull();
    }

    #endregion

    #region Error cases

    [Fact]
    public void ValidateSpecs_UnknownMatcherName_ThrowsArgumentException()
    {
        var action = () => SemanticMatcher.ValidateSpecs(
            new Dictionary<string, string> { ["id"] = "isWeird" }, null);
        action.Should().Throw<ArgumentException>().WithMessage("*isWeird*id*");
    }

    [Fact]
    public void ValidateSpecs_CustomMatcherConflictsWithBuiltIn_ThrowsArgumentException()
    {
        var action = () => SemanticMatcher.ValidateSpecs(
            new Dictionary<string, string> { ["id"] = "isUuid" },
            new Dictionary<string, SemanticMatcherFunc> { ["isUuid"] = (_, _) => null });
        action.Should().Throw<ArgumentException>().WithMessage("*isUuid*conflicts*");
    }

    [Fact]
    public void ValidateSpecs_NullSpecs_DoesNotThrow()
    {
        var action = () => SemanticMatcher.ValidateSpecs(null, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_FieldAbsentFromResponse_ThrowsInvalidOperationException()
    {
        var action = () => Apply(JToken.Parse("{'name': 'test'}"), JToken.Parse("{}"), "id", "isUuid");
        action.Should().Throw<InvalidOperationException>().WithMessage("*id*absent*");
    }

    [Fact]
    public void Apply_NullExpected_DoesNotThrow()
    {
        // When response.body is omitted in the test definition but semantic matchers are declared,
        // expected is null. Actual-field validation still runs; removal from expected is skipped.
        var result = SemanticMatcher.Apply(
            JToken.Parse("{'id': 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'}"),
            null,
            new Dictionary<string, string> { ["id"] = "isUuid" },
            null);
        result.Should().BeNull();
    }

    #endregion

    #region Custom matcher extension

    [Fact]
    public void Apply_CustomMatcherName_DelegatesToCustomFunc()
    {
        var custom = new Dictionary<string, SemanticMatcherFunc>
        {
            ["isDomainId"] = (token, _) =>
                token.Value<string>()?.StartsWith("DOM-") == true ? null : "Expected DOM-{n} format"
        };

        var result = Apply(JToken.Parse("{'code': 'DOM-42'}"), JToken.Parse("{}"), "code", "isDomainId", custom);
        result.Should().BeNull();
    }

    #endregion
}

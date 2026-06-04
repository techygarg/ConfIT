namespace ConfIT.UnitTest.Util;

public class SemanticMatcherTests
{
    #region Format matchers

    [Theory]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [InlineData("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    public void Apply_IsUuidWithValidUuid_Passes(string uuid)
    {
        // Given
        var actual   = JToken.Parse($"{{'id': '{uuid}'}}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isUuid" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsUuidWithInvalidValue_ThrowsWithFieldName()
    {
        // Given
        var actual   = JToken.Parse("{'id': 'not-a-uuid'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isUuid" }, null);
        action.Should().Throw<Exception>().WithMessage("*id*isUuid*");
    }

    [Fact]
    public void Apply_IsIsoDateWithValidDate_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'date': '2026-05-31'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["date"] = "isIsoDate" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsIsoDateWithDateTime_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'date': '2026-05-31T10:30:00Z'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["date"] = "isIsoDate" }, null);
        action.Should().Throw<Exception>().WithMessage("*date*isIsoDate*");
    }

    [Theory]
    [InlineData("2026-05-31T10:30:00Z")]
    [InlineData("2026-05-31T10:30:00+05:30")]
    [InlineData("2026-05-31T10:30:00.123Z")]
    public void Apply_IsIsoDateTimeWithValidDateTime_Passes(string dateTime)
    {
        // Given
        var actual   = JToken.Parse($"{{'createdAt': '{dateTime}'}}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["createdAt"] = "isIsoDateTime" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsIsoDateTimeWithDateOnly_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'createdAt': '2026-05-31'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["createdAt"] = "isIsoDateTime" }, null);
        action.Should().Throw<Exception>().WithMessage("*createdAt*isIsoDateTime*");
    }

    [Fact]
    public void Apply_IsEmailWithValidEmail_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'email': 'user@example.com'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["email"] = "isEmail" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsEmailWithInvalidValue_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'email': 'not-an-email'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["email"] = "isEmail" }, null);
        action.Should().Throw<Exception>().WithMessage("*email*isEmail*");
    }

    #endregion

    #region Null matchers

    [Fact]
    public void Apply_IsNullWithNullValue_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'deletedAt': null}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["deletedAt"] = "isNull" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsNullWithNonNullValue_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'deletedAt': '2026-05-31'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["deletedAt"] = "isNull" }, null);
        action.Should().Throw<Exception>().WithMessage("*deletedAt*isNull*");
    }

    [Fact]
    public void Apply_IsNotNullWithPresentValue_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'id': 1}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isNotNull" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsNotNullWithNullValue_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'id': null}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isNotNull" }, null);
        action.Should().Throw<Exception>().WithMessage("*id*isNotNull*");
    }

    #endregion

    #region Emptiness matchers

    [Fact]
    public void Apply_IsEmptyWithEmptyString_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'name': ''}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["name"] = "isEmpty" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsEmptyWithEmptyArray_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'items': []}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["items"] = "isEmpty" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsEmptyWithEmptyObject_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'meta': {}}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["meta"] = "isEmpty" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsNotEmptyWithNonEmptyArray_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'items': [1, 2]}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["items"] = "isNotEmpty" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_IsNotEmptyWithEmptyString_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'name': ''}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["name"] = "isNotEmpty" }, null);
        action.Should().Throw<Exception>().WithMessage("*name*isNotEmpty*");
    }

    [Fact]
    public void Apply_IsNotEmptyWithEmptyArray_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'items': []}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["items"] = "isNotEmpty" }, null);
        action.Should().Throw<Exception>().WithMessage("*items*isNotEmpty*");
    }

    #endregion

    #region Numeric matchers

    [Fact]
    public void Apply_GreaterThanWithValueAboveThreshold_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'count': 5}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["count"] = "greaterThan(0)" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_GreaterThanWithValueAtThreshold_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'count': 0}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["count"] = "greaterThan(0)" }, null);
        action.Should().Throw<Exception>().WithMessage("*count*greaterThan(0)*");
    }

    [Fact]
    public void Apply_GreaterThanWithNonNumericValue_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'score': 'high'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["score"] = "greaterThan(0)" }, null);
        action.Should().Throw<Exception>().WithMessage("*score*");
    }

    [Fact]
    public void Apply_LessThanWithValueBelowThreshold_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'age': 17}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["age"] = "lessThan(18)" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_LessThanWithValueAtThreshold_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'age': 18}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["age"] = "lessThan(18)" }, null);
        action.Should().Throw<Exception>().WithMessage("*age*lessThan(18)*");
    }

    #endregion

    #region Size matchers

    [Fact]
    public void Apply_HasLengthWithExactMatch_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'zip': '12345'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["zip"] = "hasLength(5)" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_HasLengthWithinInclusiveRange_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'name': 'Al'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["name"] = "hasLength(1,50)" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_HasLengthOnArray_Passes()
    {
        // Given
        var actual   = JToken.Parse("{'tags': ['a', 'b', 'c']}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["tags"] = "hasLength(3)" }, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_HasLengthWithMismatch_Throws()
    {
        // Given
        var actual   = JToken.Parse("{'zip': '1234'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["zip"] = "hasLength(5)" }, null);
        action.Should().Throw<Exception>().WithMessage("*zip*hasLength(5)*");
    }

    #endregion

    #region Nested fields

    [Fact]
    public void Apply_NestedFieldPath_PassesForValidValue()
    {
        // Given
        var actual   = JToken.Parse("{'user': {'profile': {'id': 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'}}}");
        var expected = JToken.Parse("{'user': {'profile': {}}}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["user__profile__id"] = "isUuid" }, null);
        action.Should().NotThrow();
    }

    #endregion

    #region Error cases

    [Fact]
    public void ValidateSpecs_UnknownMatcherName_ThrowsArgumentException()
    {
        // Given
        var semantic = new Dictionary<string, string> { ["id"] = "isWeird" };

        // When / Then
        var action = () => SemanticMatcher.ValidateSpecs(semantic, null);
        action.Should().Throw<ArgumentException>().WithMessage("*isWeird*id*");
    }

    [Fact]
    public void ValidateSpecs_CustomMatcherConflictsWithBuiltIn_ThrowsArgumentException()
    {
        // Given
        var semantic = new Dictionary<string, string> { ["id"] = "isUuid" };
        var custom   = new Dictionary<string, SemanticMatcherFunc> { ["isUuid"] = (_, _) => null };

        // When / Then
        var action = () => SemanticMatcher.ValidateSpecs(semantic, custom);
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
        // Given
        var actual   = JToken.Parse("{'name': 'test'}");
        var expected = JToken.Parse("{}");

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isUuid" }, null);
        action.Should().Throw<InvalidOperationException>().WithMessage("*id*absent*");
    }

    #endregion

    #region Custom matcher extension

    [Fact]
    public void Apply_CustomMatcherName_DelegatesToCustomFunc()
    {
        // Given
        var actual   = JToken.Parse("{'code': 'DOM-42'}");
        var expected = JToken.Parse("{}");
        var custom   = new Dictionary<string, SemanticMatcherFunc>
        {
            ["isDomainId"] = (token, _) =>
                token.Value<string>()?.StartsWith("DOM-") == true ? null : "Expected DOM-{n} format"
        };

        // When / Then
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["code"] = "isDomainId" }, custom);
        action.Should().NotThrow();
    }

    #endregion
}

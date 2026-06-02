namespace ConfIT.UnitTest.Util;

public class SemanticMatcherTests
{
    // ── Format matchers ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("a1b2c3d4-e5f6-7890-abcd-ef1234567890")]
    [InlineData("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    public void Apply_ShouldPass_WhenIsUuid_AndValueIsValidUuid(string uuid)
    {
        // Given
        var actual = JToken.Parse($"{{'id': '{uuid}'}}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isUuid" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenIsUuid_AndValueIsNotUuid()
    {
        // Given
        var actual = JToken.Parse("{'id': 'not-a-uuid'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isUuid" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*id*isUuid*");
    }

    [Fact]
    public void Apply_ShouldPass_WhenIsIsoDate_AndValueIsValidDate()
    {
        // Given
        var actual = JToken.Parse("{'date': '2026-05-31'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["date"] = "isIsoDate" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenIsIsoDate_AndValueIsDateTime()
    {
        // Given
        var actual = JToken.Parse("{'date': '2026-05-31T10:30:00Z'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["date"] = "isIsoDate" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*date*isIsoDate*");
    }

    [Theory]
    [InlineData("2026-05-31T10:30:00Z")]
    [InlineData("2026-05-31T10:30:00+05:30")]
    [InlineData("2026-05-31T10:30:00.123Z")]
    public void Apply_ShouldPass_WhenIsIsoDateTime_AndValueIsValidDateTime(string dateTime)
    {
        // Given
        var actual = JToken.Parse($"{{'createdAt': '{dateTime}'}}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["createdAt"] = "isIsoDateTime" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenIsIsoDateTime_AndValueIsDateOnly()
    {
        // Given
        var actual = JToken.Parse("{'createdAt': '2026-05-31'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["createdAt"] = "isIsoDateTime" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*createdAt*isIsoDateTime*");
    }

    [Fact]
    public void Apply_ShouldPass_WhenIsEmail_AndValueIsValidEmail()
    {
        // Given
        var actual = JToken.Parse("{'email': 'user@example.com'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["email"] = "isEmail" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenIsEmail_AndValueIsNotEmail()
    {
        // Given
        var actual = JToken.Parse("{'email': 'not-an-email'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["email"] = "isEmail" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*email*isEmail*");
    }

    // ── Null matchers ──────────────────────────────────────────────────────────

    [Fact]
    public void Apply_ShouldPass_WhenIsNull_AndValueIsNull()
    {
        // Given
        var actual = JToken.Parse("{'deletedAt': null}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["deletedAt"] = "isNull" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenIsNull_AndValueIsNotNull()
    {
        // Given
        var actual = JToken.Parse("{'deletedAt': '2026-05-31'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["deletedAt"] = "isNull" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*deletedAt*isNull*");
    }

    [Fact]
    public void Apply_ShouldPass_WhenIsNotNull_AndValueIsPresent()
    {
        // Given
        var actual = JToken.Parse("{'id': 1}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isNotNull" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenIsNotNull_AndValueIsNull()
    {
        // Given
        var actual = JToken.Parse("{'id': null}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isNotNull" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*id*isNotNull*");
    }

    // ── Emptiness matchers ─────────────────────────────────────────────────────

    [Fact]
    public void Apply_ShouldPass_WhenIsEmpty_AndStringIsEmpty()
    {
        // Given
        var actual = JToken.Parse("{'name': ''}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["name"] = "isEmpty" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldPass_WhenIsEmpty_AndArrayIsEmpty()
    {
        // Given
        var actual = JToken.Parse("{'items': []}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["items"] = "isEmpty" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldPass_WhenIsEmpty_AndObjectIsEmpty()
    {
        // Given
        var actual = JToken.Parse("{'meta': {}}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["meta"] = "isEmpty" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldPass_WhenIsNotEmpty_AndArrayHasElements()
    {
        // Given
        var actual = JToken.Parse("{'items': [1, 2]}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["items"] = "isNotEmpty" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenIsNotEmpty_AndStringIsEmpty()
    {
        // Given
        var actual = JToken.Parse("{'name': ''}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["name"] = "isNotEmpty" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*name*isNotEmpty*");
    }

    [Fact]
    public void Apply_ShouldFail_WhenIsNotEmpty_AndArrayIsEmpty()
    {
        // Given
        var actual = JToken.Parse("{'items': []}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["items"] = "isNotEmpty" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*items*isNotEmpty*");
    }

    // ── Numeric matchers ───────────────────────────────────────────────────────

    [Fact]
    public void Apply_ShouldPass_WhenGreaterThan_AndValueExceedsThreshold()
    {
        // Given
        var actual = JToken.Parse("{'count': 5}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["count"] = "greaterThan(0)" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenGreaterThan_AndValueDoesNotExceedThreshold()
    {
        // Given
        var actual = JToken.Parse("{'count': 0}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["count"] = "greaterThan(0)" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*count*greaterThan(0)*");
    }

    [Fact]
    public void Apply_ShouldFail_WhenGreaterThan_AndValueIsNonNumeric()
    {
        // Given
        var actual = JToken.Parse("{'score': 'high'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["score"] = "greaterThan(0)" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*score*");
    }

    [Fact]
    public void Apply_ShouldPass_WhenLessThan_AndValueIsBelowThreshold()
    {
        // Given
        var actual = JToken.Parse("{'age': 17}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["age"] = "lessThan(18)" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenLessThan_AndValueMeetsOrExceedsThreshold()
    {
        // Given
        var actual = JToken.Parse("{'age': 18}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["age"] = "lessThan(18)" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*age*lessThan(18)*");
    }

    // ── Size matchers ──────────────────────────────────────────────────────────

    [Fact]
    public void Apply_ShouldPass_WhenHasLength_ExactMatch()
    {
        // Given
        var actual = JToken.Parse("{'zip': '12345'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["zip"] = "hasLength(5)" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldPass_WhenHasLength_WithinInclusiveRange()
    {
        // Given
        var actual = JToken.Parse("{'name': 'Al'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["name"] = "hasLength(1,50)" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldPass_WhenHasLength_AppliedToArray()
    {
        // Given
        var actual = JToken.Parse("{'tags': ['a', 'b', 'c']}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["tags"] = "hasLength(3)" }, null);

        // Then
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldFail_WhenHasLength_LengthMismatch()
    {
        // Given
        var actual = JToken.Parse("{'zip': '1234'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["zip"] = "hasLength(5)" }, null);

        // Then
        action.Should().Throw<Exception>().WithMessage("*zip*hasLength(5)*");
    }

    // ── Nested fields ──────────────────────────────────────────────────────────

    [Fact]
    public void Apply_ShouldPass_WhenMatcherAppliedToNestedField()
    {
        // Given
        var actual = JToken.Parse("{'user': {'profile': {'id': 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'}}}");
        var expected = JToken.Parse("{'user': {'profile': {}}}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["user__profile__id"] = "isUuid" }, null);

        // Then
        action.Should().NotThrow();
    }

    // ── Error cases ────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateSpecs_ShouldThrow_WhenMatcherNameIsUnknown()
    {
        // Given
        var semantic = new Dictionary<string, string> { ["id"] = "isWeird" };

        // When
        var action = () => SemanticMatcher.ValidateSpecs(semantic, null);

        // Then
        action.Should().Throw<ArgumentException>().WithMessage("*isWeird*id*");
    }

    [Fact]
    public void ValidateSpecs_ShouldThrow_WhenCustomMatcherConflictsWithBuiltIn()
    {
        // Given
        var semantic = new Dictionary<string, string> { ["id"] = "isUuid" };
        var custom = new Dictionary<string, SemanticMatcherFunc> { ["isUuid"] = (_, _) => null };

        // When
        var action = () => SemanticMatcher.ValidateSpecs(semantic, custom);

        // Then
        action.Should().Throw<ArgumentException>().WithMessage("*isUuid*conflicts*");
    }

    [Fact]
    public void ValidateSpecs_ShouldPass_WhenNoSemanticMatchers()
    {
        // Given / When / Then
        var action = () => SemanticMatcher.ValidateSpecs(null, null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Apply_ShouldThrow_WhenFieldIsAbsentFromResponse()
    {
        // Given
        var actual = JToken.Parse("{'name': 'test'}");
        var expected = JToken.Parse("{}");

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["id"] = "isUuid" }, null);

        // Then
        action.Should().Throw<InvalidOperationException>().WithMessage("*id*absent*");
    }

    // ── Custom matcher extension ───────────────────────────────────────────────

    [Fact]
    public void Apply_ShouldUseCustomMatcher_WhenNameNotInBuiltIns()
    {
        // Given
        var actual = JToken.Parse("{'code': 'DOM-42'}");
        var expected = JToken.Parse("{}");
        var custom = new Dictionary<string, SemanticMatcherFunc>
        {
            ["isDomainId"] = (token, _) =>
                token.Value<string>()?.StartsWith("DOM-") == true ? null : "Expected DOM-{n} format"
        };

        // When
        var action = () => SemanticMatcher.Apply(actual, expected,
            new Dictionary<string, string> { ["code"] = "isDomainId" }, custom);

        // Then
        action.Should().NotThrow();
    }
}
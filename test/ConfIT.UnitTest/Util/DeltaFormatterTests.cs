namespace ConfIT.UnitTest.Util;

public class DeltaFormatterTests
{
    // ── Change types ───────────────────────────────────────────────────────────

    [Fact]
    public void Format_SingleModification_ShowsExpectedAndActualValues()
    {
        // Given
        var delta = JObject.Parse(@"{""name"": [""alice"", ""bob""]}");

        // When
        var result = DeltaFormatter.Format(delta);

        // Then
        result.Should().Contain("name");
        result.Should().Contain("expected:");
        result.Should().Contain("\"bob\"");
        result.Should().Contain("actual:");
        result.Should().Contain("\"alice\"");
    }

    [Fact]
    public void Format_MultipleModifications_ShowsAllFieldsAtOnce()
    {
        // Given
        var delta = JObject.Parse(@"{""name"": [""alice"", ""bob""], ""age"": [30, 31]}");

        // When
        var result = DeltaFormatter.Format(delta);

        // Then
        result.Should().Contain("name");
        result.Should().Contain("\"bob\"");
        result.Should().Contain("age");
        result.Should().Contain("31");
        result.Should().Contain("30");
    }

    [Fact]
    public void Format_FieldInActualNotInExpected_ShowsAbsentSentinel()
    {
        // Given — 3-element delta: [actualVal, 0, 0]
        var delta = JObject.Parse(@"{""debug"": [""trace"", 0, 0]}");

        // When
        var result = DeltaFormatter.Format(delta);

        // Then
        result.Should().Contain("debug");
        result.Should().Contain("expected:");
        result.Should().Contain("<absent>");
        result.Should().Contain("actual:");
        result.Should().Contain("\"trace\"");
    }

    [Fact]
    public void Format_FieldInExpectedNotInActual_ShowsMissingSentinel()
    {
        // Given — 1-element delta: [expectedVal]
        var delta = JObject.Parse(@"{""name"": [""alice""]}");

        // When
        var result = DeltaFormatter.Format(delta);

        // Then
        result.Should().Contain("name");
        result.Should().Contain("expected:");
        result.Should().Contain("\"alice\"");
        result.Should().Contain("actual:");
        result.Should().Contain("<missing>");
    }

    // ── Path formats ──────────────────────────────────────────────────────────

    [Fact]
    public void Format_NestedModification_UsesDotNotationPath()
    {
        // Given
        var delta = JObject.Parse(@"{""user"": {""address"": {""city"": [""London"", ""Paris""]}}}");

        // When
        var result = DeltaFormatter.Format(delta);

        // Then
        result.Should().Contain("user.address.city");
        result.Should().Contain("\"Paris\"");
        result.Should().Contain("\"London\"");
    }

    [Fact]
    public void Format_ArrayElementModification_UsesIndexPath()
    {
        // Given
        var delta = JObject.Parse(@"{""items"": {""_t"": ""a"", ""1"": {""id"": [99, 2]}}}");

        // When
        var result = DeltaFormatter.Format(delta);

        // Then
        result.Should().Contain("items[1].id");
        result.Should().Contain("2");
        result.Should().Contain("99");
    }

    // ── Output structure ──────────────────────────────────────────────────────

    [Fact]
    public void Format_AnyDelta_ContainsHeader()
    {
        // Given
        var delta = JObject.Parse(@"{""x"": [1, 2]}");

        // When
        var result = DeltaFormatter.Format(delta);

        // Then
        result.Should().Contain("Response body mismatch:");
    }

    [Fact]
    public void Format_EmptyDelta_ReturnsEmptyString()
    {
        // Given
        var delta = JObject.Parse("{}");

        // When / Then
        DeltaFormatter.Format(delta).Should().BeEmpty();
    }
}

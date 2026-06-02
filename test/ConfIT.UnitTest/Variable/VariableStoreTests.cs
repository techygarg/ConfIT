namespace ConfIT.UnitTest.Variable;

public class VariableStoreTests
{
    [Fact]
    public void Set_AndResolveShortName_ReturnsValue()
    {
        // Given
        var store = new VariableStore();
        store.Set("ShouldCreateUser", "userId", new JValue("abc-123"));

        // When
        var result = store.Resolve("userId");

        // Then
        result.Value<string>().Should().Be("abc-123");
    }

    [Fact]
    public void Set_AndResolveFullPrefix_ReturnsValue()
    {
        // Given
        var store = new VariableStore();
        store.Set("ShouldCreateUser", "userId", new JValue("abc-123"));

        // When
        var result = store.Resolve("ShouldCreateUser", "userId");

        // Then
        result.Value<string>().Should().Be("abc-123");
    }

    [Fact]
    public void Set_SameTestNameAndVarNameTwice_ThrowsCollisionException()
    {
        // Given
        var store = new VariableStore();
        store.Set("ShouldCreateUser", "userId", new JValue("abc-123"));

        // When
        var act = () => store.Set("ShouldCreateUser", "userId", new JValue("xyz-456"));

        // Then
        act.Should().Throw<VariableCollisionException>()
            .WithMessage("*userId*ShouldCreateUser*");
    }

    [Fact]
    public void Set_SameVarNameDifferentTests_ThrowsAmbiguousOnShortResolve()
    {
        // Given
        var store = new VariableStore();
        store.Set("ShouldCreateUser", "userId", new JValue("abc-123"));
        store.Set("ShouldCreateAnotherUser", "userId", new JValue("xyz-456"));

        // When
        var act = () => store.Resolve("userId");

        // Then
        act.Should().Throw<AmbiguousVariableException>()
            .WithMessage("*userId*ShouldCreateUser*ShouldCreateAnotherUser*");
    }

    [Fact]
    public void Set_SameVarNameDifferentTests_FullPrefixResolvesEachIndependently()
    {
        // Given
        var store = new VariableStore();
        store.Set("ShouldCreateUser", "userId", new JValue("abc-123"));
        store.Set("ShouldCreateAnotherUser", "userId", new JValue("xyz-456"));

        // When / Then
        store.Resolve("ShouldCreateUser", "userId").Value<string>().Should().Be("abc-123");
        store.Resolve("ShouldCreateAnotherUser", "userId").Value<string>().Should().Be("xyz-456");
    }

    [Fact]
    public void Resolve_UndefinedShortName_ThrowsUndefinedException()
    {
        // Given
        var store = new VariableStore();

        // When
        var act = () => store.Resolve("userId");

        // Then
        act.Should().Throw<UndefinedVariableException>().WithMessage("*userId*");
    }

    [Fact]
    public void Resolve_UndefinedFullPrefix_ThrowsUndefinedException()
    {
        // Given
        var store = new VariableStore();

        // When
        var act = () => store.Resolve("ShouldCreateUser", "userId");

        // Then
        act.Should().Throw<UndefinedVariableException>().WithMessage("*ShouldCreateUser*userId*");
    }

    [Fact]
    public void Set_MultipleVarsSameTest_AllResolveIndependently()
    {
        // Given
        var store = new VariableStore();
        store.Set("ShouldCreateUser", "userId", new JValue("abc-123"));
        store.Set("ShouldCreateUser", "userEmail", new JValue("alice@example.com"));

        // When / Then
        store.Resolve("userId").Value<string>().Should().Be("abc-123");
        store.Resolve("userEmail").Value<string>().Should().Be("alice@example.com");
    }

    [Fact]
    public void Set_PreservesJTokenType_Number()
    {
        // Given
        var store = new VariableStore();
        store.Set("ShouldCreateUser", "score", new JValue(42));

        // When
        var result = store.Resolve("score");

        // Then
        result.Type.Should().Be(JTokenType.Integer);
        result.Value<int>().Should().Be(42);
    }
}
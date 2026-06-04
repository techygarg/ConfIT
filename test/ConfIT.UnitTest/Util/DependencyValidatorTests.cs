using System.IO;

namespace ConfIT.UnitTest.Util;

public class DependencyValidatorTests
{
    private static IReadOnlyList<(string Name, JToken Token)> Build(params string[] names)
        => names.Select(n => (n, (JToken)new JObject())).ToList();

    private static IReadOnlyList<(string Name, JToken Token)> BuildWithDeps(
        params (string Name, string[]? Depends)[] tests)
        => tests.Select(t =>
        {
            var obj = new JObject();
            if (t.Depends?.Length > 0)
                obj["depends"] = new JArray(t.Depends.Cast<object>().ToArray());
            return (t.Name, (JToken)obj);
        }).ToList();

    #region Valid cases

    [Fact]
    public void Validate_NoDependencies_DoesNotThrow()
    {
        var tests = Build("TestA", "TestB", "TestC");
        var act = () => DependencyValidator.Validate(tests, "file.json");
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ValidSingleDep_DoesNotThrow()
    {
        var tests = BuildWithDeps(
            ("ShouldCreateUser", null),
            ("ShouldFetchUser", ["ShouldCreateUser"]));
        var act = () => DependencyValidator.Validate(tests, "file.json");
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_ValidMultipleDeps_DoesNotThrow()
    {
        var tests = BuildWithDeps(
            ("TestA", null),
            ("TestB", null),
            ("TestC", ["TestA", "TestB"]));
        var act = () => DependencyValidator.Validate(tests, "file.json");
        act.Should().NotThrow();
    }

    #endregion

    #region Unknown dep name

    [Fact]
    public void Validate_UnknownDepName_ThrowsWithFileAndTestAndDepInMessage()
    {
        // Given
        var tests = BuildWithDeps(
            ("ShouldCreateUser", null),
            ("ShouldFetchUser", ["ShouldCrateUser"]));

        // When
        var act = () => DependencyValidator.Validate(tests, "my-tests.json");

        // Then
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*ShouldFetchUser*")
            .WithMessage("*ShouldCrateUser*")
            .WithMessage("*my-tests.json*");
    }

    #endregion

    #region Forward reference

    [Fact]
    public void Validate_ForwardReference_ThrowsWithFileAndTestAndDepInMessage()
    {
        // Given — ShouldFetchUser is defined first but references ShouldCreateUser (defined after)
        var tests = BuildWithDeps(
            ("ShouldFetchUser", ["ShouldCreateUser"]),
            ("ShouldCreateUser", null));

        // When
        var act = () => DependencyValidator.Validate(tests, "my-tests.json");

        // Then
        act.Should().Throw<InvalidDataException>()
            .WithMessage("*ShouldFetchUser*")
            .WithMessage("*ShouldCreateUser*")
            .WithMessage("*my-tests.json*");
    }

    [Fact]
    public void Validate_SelfReference_Throws()
    {
        // Given — a test cannot depend on itself
        var tests = BuildWithDeps(("TestA", ["TestA"]));
        var act = () => DependencyValidator.Validate(tests, "file.json");
        act.Should().Throw<InvalidDataException>();
    }

    #endregion
}

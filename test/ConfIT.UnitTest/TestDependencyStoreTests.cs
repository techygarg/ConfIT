namespace ConfIT.UnitTest;

public class TestDependencyStoreTests
{
    private static TestDependencyStore Store() => new();

    #region CheckPrerequisites — no deps

    [Fact]
    public void CheckPrerequisites_NullDepends_ReturnsNull()
    {
        var store = Store();
        store.CheckPrerequisites(null).Should().BeNull();
    }

    [Fact]
    public void CheckPrerequisites_EmptyDepends_ReturnsNull()
    {
        var store = Store();
        store.CheckPrerequisites([]).Should().BeNull();
    }

    #endregion

    #region CheckPrerequisites — all passed

    [Fact]
    public void CheckPrerequisites_SingleDepPassed_ReturnsNull()
    {
        // Given
        var store = Store();
        store.RecordStatus("TestA", TestRunStatus.Passed);

        // When / Then
        store.CheckPrerequisites(["TestA"]).Should().BeNull();
    }

    [Fact]
    public void CheckPrerequisites_MultipleDepsPassed_ReturnsNull()
    {
        // Given
        var store = Store();
        store.RecordStatus("TestA", TestRunStatus.Passed);
        store.RecordStatus("TestB", TestRunStatus.Passed);

        // When / Then
        store.CheckPrerequisites(["TestA", "TestB"]).Should().BeNull();
    }

    #endregion

    #region CheckPrerequisites — blocked

    [Fact]
    public void CheckPrerequisites_DepFailed_ReturnsDepInfo()
    {
        // Given
        var store = Store();
        store.RecordStatus("TestA", TestRunStatus.Failed);

        // When
        var result = store.CheckPrerequisites(["TestA"]);

        // Then
        result.Should().NotBeNull();
        result!.Value.Name.Should().Be("TestA");
        result.Value.Status.Should().Be(TestRunStatus.Failed);
    }

    [Fact]
    public void CheckPrerequisites_DepSkipped_ReturnsDepInfo()
    {
        // Given
        var store = Store();
        store.RecordStatus("TestA", TestRunStatus.Skipped);

        // When
        var result = store.CheckPrerequisites(["TestA"]);

        // Then
        result!.Value.Name.Should().Be("TestA");
        result.Value.Status.Should().Be(TestRunStatus.Skipped);
    }

    [Fact]
    public void CheckPrerequisites_MultipleDeps_ReturnsFirstNotPassed()
    {
        // Given
        var store = Store();
        store.RecordStatus("TestA", TestRunStatus.Passed);
        store.RecordStatus("TestB", TestRunStatus.Failed);
        store.RecordStatus("TestC", TestRunStatus.Passed);

        // When
        var result = store.CheckPrerequisites(["TestA", "TestB", "TestC"]);

        // Then
        result!.Value.Name.Should().Be("TestB");
    }

    #endregion

    #region CheckPrerequisites — invariant violation

    [Fact]
    public void CheckPrerequisites_AbsentDep_ThrowsInvalidOperationException()
    {
        // Given
        var store = Store();

        // When
        var act = () => store.CheckPrerequisites(["NonExistent"]);

        // Then
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NonExistent*");
    }

    #endregion

    #region RecordStatus

    [Fact]
    public void RecordStatus_OverwritesPreviousEntry()
    {
        // Given
        var store = Store();
        store.RecordStatus("TestA", TestRunStatus.Failed);

        // When
        store.RecordStatus("TestA", TestRunStatus.Passed);

        // Then
        store.CheckPrerequisites(["TestA"]).Should().BeNull();
    }

    #endregion
}

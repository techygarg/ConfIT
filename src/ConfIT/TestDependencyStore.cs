using System.Collections.Concurrent;

namespace ConfIT;

public sealed class TestDependencyStore
{
    public static readonly TestDependencyStore Instance = new();

    private readonly ConcurrentDictionary<string, TestRunStatus> _status = new();

    public void RecordStatus(string testName, TestRunStatus status)
        => _status[testName] = status;

    public (string Name, TestRunStatus Status)? CheckPrerequisites(List<string>? depends)
    {
        if (depends is not { Count: > 0 }) return null;

        foreach (var dep in depends)
        {
            if (!_status.TryGetValue(dep, out var status))
                throw new InvalidOperationException(
                    $"Prerequisite '{dep}' has no recorded status. " +
                    "This indicates an execution order violation — " +
                    "load-time validation should have caught this dependency.");

            if (status != TestRunStatus.Passed)
                return (dep, status);
        }

        return null;
    }
}

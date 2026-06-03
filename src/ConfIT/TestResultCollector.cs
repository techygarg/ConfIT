using ConfIT.Util;

namespace ConfIT;

public sealed class TestResultCollector : IDisposable
{
    private const string Sep = "══════════════════════════════════════════════════════";
    private const string Div = "──────────────────────────────────────────────────────";
    private readonly object _lock = new();

    private readonly List<Result> _results = new();
    private bool _summaryPrinted;

    public void Dispose()
    {
        PrintSummary();
    }

    public void Record(string name, TestRunStatus status, TimeSpan? duration = null, string? sourceFile = null, string? reason = null)
    {
        lock (_lock)
        {
            _results.Add(new Result(name, status, duration, sourceFile, reason));
        }
    }

    private void PrintSummary()
    {
        if (_results.Count == 0 || _summaryPrinted) return;
        _summaryPrinted = true;

        var passed    = _results.Count(r => r.Status == TestRunStatus.Passed);
        var failed    = _results.Count(r => r.Status == TestRunStatus.Failed);
        var skipped   = _results.Count(r => r.Status == TestRunStatus.Skipped);
        var nameWidth = _results.Max(r => r.Name.Length);

        Console.WriteLine();
        Console.WriteLine(TestColor.Structure(Sep));
        Console.WriteLine($"  {TestColor.Emphasis("Suite Summary")}");
        Console.WriteLine(TestColor.Structure(Sep));
        Console.WriteLine();

        var groups = _results
            .GroupBy(r => r.SourceFile ?? string.Empty)
            .ToList();

        foreach (var group in groups)
        {
            if (!string.IsNullOrEmpty(group.Key))
                Console.WriteLine($"  {TestColor.Info(group.Key)}");

            foreach (var r in group)
            {
                var (icon, label) = r.Status switch
                {
                    TestRunStatus.Passed => (TestColor.Expected("✓"), TestColor.Emphasis(r.Name.PadRight(nameWidth))),
                    TestRunStatus.Failed => (TestColor.Error("✗"), TestColor.Error(r.Name.PadRight(nameWidth))),
                    _                   => (TestColor.Subtle("⏭"), TestColor.Subtle(r.Name.PadRight(nameWidth)))
                };
                var duration = r.Duration.HasValue
                    ? TestColor.Subtle(FormatDuration(r.Duration.Value).PadLeft(8))
                    : new string(' ', 8);

                Console.WriteLine($"    {icon}  {label}  {duration}");

                if (r.Reason is not null)
                    Console.WriteLine($"         {TestColor.Subtle($"└─ {r.Reason}")}");
            }

            Console.WriteLine();
        }

        Console.WriteLine(TestColor.Subtle(Div));

        var passedLabel  = passed > 0
            ? TestColor.Expected($"✓ {passed} passed")
            : TestColor.Subtle($"✓ {passed} passed");
        var failedLabel  = failed > 0
            ? TestColor.Error($"✗ {failed} failed")
            : TestColor.Subtle($"✗ {failed} failed");
        var skippedLabel = TestColor.Subtle($"⏭ {skipped} skipped");

        Console.WriteLine($"  Total: {_results.Count}   {passedLabel}   {failedLabel}   {skippedLabel}");
        Console.WriteLine(TestColor.Subtle(Div));
        Console.WriteLine();
    }

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalMilliseconds < 1) return "< 1ms";
        if (d.TotalSeconds < 1) return $"{(int)d.TotalMilliseconds}ms";
        return $"{d.TotalSeconds:F1}s";
    }

    private sealed record Result(string Name, TestRunStatus Status, TimeSpan? Duration, string? SourceFile, string? Reason);
}

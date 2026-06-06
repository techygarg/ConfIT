namespace ConfIT.Reporting;

/// <summary>
///     Semantic ANSI colour helpers for test console output.
///     Callers express intent (Error, Expected, Actual…) rather than
///     raw colour names — change a colour here and it propagates everywhere.
///     Colour support: on by default; opt out via the NO_COLOR env var
///     (https://no-color.org) or by setting TERM=dumb.
///     Console.IsOutputRedirected is intentionally NOT checked — dotnet test
///     always redirects stdout through its pipe even when running in a real TTY.
/// </summary>
public static class TestColor
{
    private static readonly bool Enabled =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")) &&
        Environment.GetEnvironmentVariable("TERM") != "dumb";

    /// <summary>Error headers, assertion failures, mismatch indicators.</summary>
    public static string Error(string s)
    {
        return Apply(s, "1;31");
        // bold red
    }

    /// <summary>Expected / target values — what the test wants to see.</summary>
    public static string Expected(string s)
    {
        return Apply(s, "32");
        // green
    }

    /// <summary>Actual values that differ from expected — the problem value.</summary>
    public static string Actual(string s)
    {
        return Apply(s, "31");
        // red
    }

    /// <summary>Neutral informational labels (e.g. "Actual:" body label).</summary>
    public static string Info(string s)
    {
        return Apply(s, "33");
        // yellow
    }

    /// <summary>Structural UI elements: separators, navigation markers.</summary>
    public static string Structure(string s)
    {
        return Apply(s, "36");
        // cyan
    }

    /// <summary>Strong emphasis: test names, diff field paths.</summary>
    public static string Emphasis(string s)
    {
        return Apply(s, "1");
        // bold
    }

    /// <summary>De-emphasised secondary content: matchers, footer, sentinels.</summary>
    public static string Subtle(string s)
    {
        return Apply(s, "90");
        // dim
    }

    private static string Apply(string s, string code)
    {
        return Enabled ? $"\x1b[{code}m{s}\x1b[0m" : s;
    }
}

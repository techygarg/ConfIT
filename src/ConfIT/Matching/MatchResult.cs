namespace ConfIT.Matching;

public sealed record MatchResult(bool Passed, string? Description)
{
    public static MatchResult Ok { get; } = new(true, null);
}

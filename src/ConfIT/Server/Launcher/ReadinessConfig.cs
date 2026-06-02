namespace ConfIT.Server.Launcher;

public sealed class ReadinessConfig
{
    // Exactly one of Url (HTTP probe) or Port (TCP probe) must be set.
    public string? Url { get; init; }
    public int? Port { get; init; }
    public int TimeoutSeconds { get; init; } = 30;
    public int IntervalMs { get; init; } = 500;
}
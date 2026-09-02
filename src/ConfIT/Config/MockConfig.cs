namespace ConfIT.Config;

public sealed class MockConfig
{
    public string? Url { get; set; }

    /// <summary>
    /// Print every request WireMock receives, and whether it matched a stub.
    /// Switch on to discover what the service under test actually calls: run a test with no
    /// <c>mock:</c> block and read the unmatched requests.
    /// </summary>
    public bool EnableLogs { get; set; }
}

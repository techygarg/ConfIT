namespace ConfIT.Config;

public class SuiteConfig
{
    public string MockServerUrl { get; set; } = string.Empty;
    public bool EnableMockServerLogs { get; set; }
    public string ApiServerUrl { get; set; } = string.Empty;
    public string ApiResponseFolder { get; set; } = string.Empty;
    public string RequestBodyFolder { get; set; } = string.Empty;
    public string ResponseBodyFolder { get; set; } = string.Empty;
    public Dictionary<string, SemanticMatcherFunc> CustomMatchers { get; set; } = new();
}
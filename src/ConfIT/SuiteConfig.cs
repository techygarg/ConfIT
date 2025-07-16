namespace ConfIT
{
    public class SuiteConfig
    {
        public string MockServerUrl { get; set; } = string.Empty;
        public bool EnableMockServerLogs { get; set; } = false;
        public string ApiServerUrl { get; set; } = string.Empty;
        public string ApiResponseFolder { get; set; } = string.Empty;
        public string RequestBodyFolder { get; set; } = string.Empty;
        public string ResponseBodyFolder { get; set; } = string.Empty;
    }
}
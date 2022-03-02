namespace ConfIT
{
    public class SuiteConfig
    {
        public string MockServerUrl { get; set; }
        public bool EnableMockServerLogs { get; set; }
        public string ApiServerUrl { get; set; }
        public string ApiResponseFolder { get; set; }
        public string RequestBodyFolder { get; set; }
        public string ResponseBodyFolder { get; set; }
    }
}
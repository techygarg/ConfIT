namespace ConfIT.Server.Dto
{
    public class HttpTestResponse : BaseRequestResponse
    {
        public int StatusCode { get; set; }
        public Matcher Matcher { get; set; }
    }
}
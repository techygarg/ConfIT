namespace ConfIT.Model;

public class HttpTestRequest : BaseRequestResponse
{
    public string Method { get; set; }
    public string Path { get; set; }
    public Dictionary<string, string> Params { get; set; }
}

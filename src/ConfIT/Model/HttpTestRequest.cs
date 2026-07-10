namespace ConfIT.Model;

public class HttpTestRequest : HttpPayload
{
    public string Method { get; set; }
    public string Path { get; set; }
    public Dictionary<string, string>? Params { get; set; }
    public GraphqlRequest? Graphql { get; set; }
}

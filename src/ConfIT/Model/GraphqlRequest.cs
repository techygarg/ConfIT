namespace ConfIT.Model;

public class GraphqlRequest
{
    public string? Query { get; set; }
    public string? QueryFromFile { get; set; }
    public JToken? Variables { get; set; }
    public string? OperationName { get; set; }
}

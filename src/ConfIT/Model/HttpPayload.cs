namespace ConfIT.Model;

public abstract class HttpPayload
{
    public string? BodyFromFile { get; set; }
    public JToken? Body { get; set; }
    public JToken? Override { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

namespace ConfIT.Model;

public class Matcher
{
    public List<string>? Ignore { get; set; }
    public Dictionary<string, string>? Pattern { get; set; }
    public Dictionary<string, string>? Semantic { get; set; }
}

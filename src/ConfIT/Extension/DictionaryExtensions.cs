namespace ConfIT.Extension;

public static class DictionaryExtensions
{
    public static string DictionaryToString(this Dictionary<string, string> dictionary)
    {
        return "{" + string.Join(", ", dictionary.Select(kvp => $"{kvp.Key} : {kvp.Value}")) + "}";
    }
}
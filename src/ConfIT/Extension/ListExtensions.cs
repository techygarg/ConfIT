namespace ConfIT.Extension;

public static class ListExtensions
{
    public static string ListToString(this List<string> list)
    {
        return "{" + string.Join(", ", list) + "}";
    }
}
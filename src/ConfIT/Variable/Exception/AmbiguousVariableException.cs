namespace ConfIT.Variable.Exception;

public class AmbiguousVariableException : InvalidOperationException
{
    public AmbiguousVariableException(string varName, IEnumerable<string> testNames)
        : base(BuildMessage(varName, testNames)) { }

    private static string BuildMessage(string varName, IEnumerable<string> testNames)
    {
        var names = testNames.ToList();
        var suggestions = string.Join(" or ", names.Select(t => $"{{{{{t}.{varName}}}}}"));
        return $"Variable '{varName}' is ambiguous — extracted by: {string.Join(", ", names)}. " +
               $"Use full prefix: {suggestions}";
    }
}

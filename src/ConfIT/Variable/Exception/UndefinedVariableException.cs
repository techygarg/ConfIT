namespace ConfIT.Variable.Exception;

public class UndefinedVariableException : InvalidOperationException
{
    public static UndefinedVariableException ForShortName(string varName) =>
        new($"Variable '{varName}' has not been extracted by any test yet. " +
            $"Ensure the test that extracts '{varName}' runs before this one.");

    public static UndefinedVariableException ForFullPrefix(string testName, string varName) =>
        new($"Test '{testName}' has not extracted a variable named '{varName}'.");

    public static UndefinedVariableException ForEnvironmentVariable(string envVar) =>
        new($"Environment variable '${{{envVar}}}' is not set.");

    private UndefinedVariableException(string message) : base(message) { }
}

namespace ConfIT.Variable.Exception;

public class UndefinedVariableException : InvalidOperationException
{
    private UndefinedVariableException(string message) : base(message)
    {
    }

    public static UndefinedVariableException ForShortName(string varName)
    {
        return new UndefinedVariableException($"Variable '{varName}' has not been extracted by any test yet. " +
                                              $"Ensure the test that extracts '{varName}' runs before this one.");
    }

    public static UndefinedVariableException ForFullPrefix(string testName, string varName)
    {
        return new UndefinedVariableException($"Test '{testName}' has not extracted a variable named '{varName}'.");
    }

    public static UndefinedVariableException ForEnvironmentVariable(string envVar)
    {
        return new UndefinedVariableException($"Environment variable '${{{envVar}}}' is not set.");
    }
}
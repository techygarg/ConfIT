namespace ConfIT.Variable.Exception
{
    // Thrown when the same variable name is referenced with both {{varName}} and ${varName} syntax
    // within the same test case. Detection is deferred to v2 static validation utility.
    public class VariableConfigurationException : System.InvalidOperationException
    {
        public VariableConfigurationException(string varName)
            : base($"Variable '{varName}' is referenced using both '{{{{varName}}}}' (runtime) " +
                   $"and '${{varName}}' (environment) syntax. Use one syntax consistently.") { }
    }
}

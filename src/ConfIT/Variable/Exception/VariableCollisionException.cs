namespace ConfIT.Variable.Exception
{
    public class VariableCollisionException : System.InvalidOperationException
    {
        public VariableCollisionException(string testName, string varName)
            : base($"Variable '{varName}' already extracted by test '{testName}'. " +
                   $"Each variable name can only be extracted once per test. Use a distinct name.") { }
    }
}

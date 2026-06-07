namespace ConfIT.Runner.Boot;

public sealed class AppLauncherException : Exception
{
    public AppLauncherException(string message) : base(message)
    {
    }
}

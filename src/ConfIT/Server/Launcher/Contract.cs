namespace ConfIT.Server.Launcher;

internal interface IReadinessProbe : IDisposable
{
    bool TryProbe();
}


public sealed class AppLauncherException : Exception
{
    public AppLauncherException(string message) : base(message) { }
}


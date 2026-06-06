namespace ConfIT.Runner.Boot;

internal interface IReadinessProbe : IDisposable
{
    bool TryProbe();
}

using ConfIT.Reporting;
using ConfIT.Runner.Http;

namespace ConfIT;

/// <summary>
/// A fully-wired, disposable test suite returned by <see cref="SuiteBootstrapper"/>.
/// Owns the lifecycle of any started infrastructure (in-process server, external process)
/// and the result collector. Pass <see cref="Context"/> to
/// <see cref="BaseTest(TestSuiteContext, Contract.ITestOutputLogger?)"/>.
/// </summary>
public sealed class BootstrappedSuite : IDisposable
{
    private readonly IDisposable?        _infrastructure;
    private readonly TestResultCollector _resultCollector;
    private readonly TestHttpClient?     _ownedHttpClient;
    private bool                         _disposed;

    /// <summary>
    /// The runtime context to pass to <see cref="BaseTest"/>.
    /// </summary>
    public TestSuiteContext Context { get; }

    /// <summary>
    /// Service provider from the in-process host. Available only for
    /// in-process component suites (<see cref="SuiteBootstrapper.ForComponent{TStartup}"/>).
    /// Null for command and integration suites.
    /// </summary>
    public IServiceProvider? Services { get; }

    internal BootstrappedSuite(
        TestSuiteContext    context,
        IDisposable?        infrastructure,
        TestResultCollector resultCollector,
        IServiceProvider?   services        = null,
        TestHttpClient?     ownedHttpClient = null)
    {
        Context          = context;
        _infrastructure  = infrastructure;
        _resultCollector = resultCollector;
        Services         = services;
        _ownedHttpClient = ownedHttpClient;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Print the suite summary before tearing down infrastructure.
        _resultCollector.Dispose();
        _infrastructure?.Dispose();
        _ownedHttpClient?.Dispose();
    }
}

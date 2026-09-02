using System.IO;
using ConfIT.Config;
using ConfIT.Extension;
using ConfIT.Matching;
using ConfIT.Reporting;
using ConfIT.Runner.Boot;
using ConfIT.Runner.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ConfIT;

/// <summary>
/// Creates fully-wired <see cref="BootstrappedSuite"/> instances from a YAML config file.
/// Handles the adapter chain (<c>ToSuiteConfig</c>, <c>ToTestFilter</c>,
/// <c>ToAuthTokenProvider</c>), folder creation, and infrastructure startup internally —
/// consumers only write project-specific setup (e.g. DB seeding).
/// </summary>
public static class SuiteBootstrapper
{
    /// <summary>
    /// Bootstraps a component test suite where the application under test runs in-process
    /// via <see cref="TestSuiteInitializer{TStartup}"/>.
    /// </summary>
    /// <remarks>
    /// If the YAML auth block uses <c>oauth2-client-credentials</c>, the token endpoint
    /// must be reachable before this method is called (i.e. any stub WireMock server
    /// must already be running — the caller owns that lifecycle). The token request happens
    /// once, eagerly, before the in-process host starts.
    /// </remarks>
    /// <typeparam name="TStartup">The application's entry-point class (Program or Startup).</typeparam>
    /// <param name="configFile">Path to the suite YAML config file (e.g. "suite.config.yaml").</param>
    /// <param name="configureServices">Optional DI overrides applied to the in-process host.</param>
    /// <param name="onStarted">
    /// Optional callback invoked after the host is ready — use for DB seeding and other
    /// project-specific initialisation that needs the service container.
    /// </param>
    /// <param name="customMatchers">Optional custom semantic matchers to register.</param>
    public static BootstrappedSuite ForComponent<TStartup>(
        string configFile,
        Action<IServiceCollection>?                      configureServices = null,
        Action<IServiceProvider>?                        onStarted         = null,
        Dictionary<string, SemanticMatcherFunc>?         customMatchers    = null)
        where TStartup : class
    {
        var cfg          = SuiteConfiguration.LoadComponent(configFile);
        var authProvider = cfg.ToAuthTokenProvider();
        var initializer  = new TestSuiteInitializer<TStartup>(cfg.Startup.Settings!, configureServices, authProvider);

        onStarted?.Invoke(initializer.Services);

        var suiteConfig      = Hydrate(cfg.ToSuiteConfig(), customMatchers);
        var resultCollector  = new TestResultCollector();

        var context = new TestSuiteContext(
            HttpClient:      initializer.TestHttpClient,
            Config:          suiteConfig,
            Filter:          cfg.ToTestFilter(),
            ResultCollector: resultCollector);

        return new BootstrappedSuite(context, initializer, resultCollector, initializer.Services);
    }

    /// <summary>
    /// Bootstraps a component test suite where the application under test is launched as
    /// an external process via <see cref="AppLauncher"/>.
    /// </summary>
    /// <remarks>
    /// If the YAML auth block uses <c>oauth2-client-credentials</c>, the token endpoint
    /// must be reachable before this method is called (i.e. any stub WireMock server
    /// must already be running — the caller owns that lifecycle).
    /// </remarks>
    public static BootstrappedSuite ForCommand(
        string configFile,
        Dictionary<string, SemanticMatcherFunc>? customMatchers = null)
    {
        var cfg          = SuiteConfiguration.LoadComponent(configFile);
        var launcher     = AppLauncher.Start(cfg.ToAppLauncherConfig());
        var authProvider = cfg.ToAuthTokenProvider();
        var httpClient   = TestHttpClient.Create(cfg.Api.Url!, authProvider);

        var suiteConfig     = Hydrate(cfg.ToSuiteConfig(), customMatchers);
        var resultCollector = new TestResultCollector();

        var context = new TestSuiteContext(
            HttpClient:      httpClient,
            Config:          suiteConfig,
            Filter:          cfg.ToTestFilter(),
            ResultCollector: resultCollector);

        return new BootstrappedSuite(context, launcher, resultCollector, ownedHttpClient: httpClient);
    }

    /// <summary>
    /// Bootstraps an integration test suite that points at an already-running service.
    /// </summary>
    /// <param name="configFile">Path to the suite YAML config file.</param>
    /// <param name="environment">
    /// Optional environment name (e.g. "staging"). Falls back to the
    /// <c>TEST_ENVIRONMENT</c> env var, then the YAML <c>default</c> property.
    /// </param>
    /// <param name="customMatchers">Optional custom semantic matchers to register.</param>
    public static BootstrappedSuite ForIntegration(
        string configFile,
        string? environment                                  = null,
        Dictionary<string, SemanticMatcherFunc>? customMatchers = null)
    {
        var cfg          = SuiteConfiguration.LoadIntegration(configFile, environment);
        var authProvider = cfg.ToAuthTokenProvider();
        var httpClient   = TestHttpClient.Create(cfg.Api.Url!, authProvider);

        var suiteConfig     = Hydrate(cfg.ToSuiteConfig(), customMatchers);
        var resultCollector = new TestResultCollector();

        var context = new TestSuiteContext(
            HttpClient:      httpClient,
            Config:          suiteConfig,
            Filter:          cfg.ToTestFilter(),
            ResultCollector: resultCollector);

        return new BootstrappedSuite(context, infrastructure: null, resultCollector, ownedHttpClient: httpClient);
    }

    // Resolves the response folder to an absolute path and ensures it exists.
    // Applies custom matchers when provided.
    private static SuiteConfig Hydrate(
        SuiteConfig                              config,
        Dictionary<string, SemanticMatcherFunc>? customMatchers)
    {
        if (!string.IsNullOrWhiteSpace(config.ApiResponseFolder))
        {
            var fullPath = Directory
                .CreateDirectory(Path.GetFullPath(
                    Path.Combine(Environment.CurrentDirectory, config.ApiResponseFolder)))
                .FullName;
            config.ApiResponseFolder = fullPath;
        }

        if (customMatchers is not null)
            config.CustomMatchers = customMatchers;

        return config;
    }
}

using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace ConfIT.Server.Http;

public class TestSuiteInitializer<TProgram> : IDisposable where TProgram : class
{
    private readonly InternalFactory _factory;

    public TestSuiteInitializer(string settingsFile, Action<IServiceCollection>? configureServices = null)
    {
        if (string.IsNullOrWhiteSpace(settingsFile))
            throw new ArgumentException("Please provide app settings file name", nameof(settingsFile));

        _factory = new InternalFactory(settingsFile, configureServices);
        TestHttpClient = new TestHttpClient(_factory.CreateClient());
    }

    public TestHttpClient TestHttpClient { get; }

    public IServiceProvider Services => _factory.Services;

    [Obsolete("Use Services (IServiceProvider) instead. TestServer is an implementation detail of the legacy hosting model.")]
    public TestServer TestServer => _factory.Server;

    public void Dispose() => _factory.Dispose();

    private sealed class InternalFactory : WebApplicationFactory<TProgram>
    {
        private readonly string _settingsFilePath;
        private readonly Action<IServiceCollection>? _configureServices;

        internal InternalFactory(string settingsFile, Action<IServiceCollection>? configureServices)
        {
            // Resolve to absolute path immediately — WebApplicationFactory sets content root
            // to the app's source directory, so relative paths must be anchored here while
            // the working directory is still the test output directory.
            _settingsFilePath = Path.GetFullPath(settingsFile);
            _configureServices = configureServices;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.Sources.Clear();
                config.AddJsonFile(
                    new PhysicalFileProvider(Path.GetDirectoryName(_settingsFilePath)!),
                    Path.GetFileName(_settingsFilePath),
                    optional: false,
                    reloadOnChange: false);
            });

            if (_configureServices is not null)
                builder.ConfigureServices(_configureServices);
        }
    }
}

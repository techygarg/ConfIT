using System;
using System.IO;
using ConfIT;
using ConfIT.Config;
using ConfIT.Extension;
using ConfIT.Server.Http;
// 'Launcher' alias is required: the project namespace is 'User.ComponentTests.AppLauncher',
// which means the bare name 'AppLauncher' resolves to that namespace, not the class.
using Launcher = ConfIT.Server.Launcher.AppLauncher;

namespace User.ComponentTests.AppLauncher.SetUp
{
    /// <summary>
    /// Demonstrates the AppLauncher (command) mode.
    ///
    /// Compare with User.ComponentTests/SetUp/TestSuiteFixture.cs (in-process mode):
    ///   - No TestSuiteInitializer — the app runs as a separate process
    ///   - No InitializeDb — the app seeds its own database on startup
    ///   - No User.Api project reference — the test project knows nothing about
    ///     the app's internals; it only speaks HTTP
    ///   - Launcher starts the process, waits for TCP readiness, and stops it on Dispose
    /// </summary>
    public class TestSuiteFixture : IDisposable
    {
        private readonly Launcher _launcher;

        public TestSuiteFixture()
        {
            var cfg = SuiteConfiguration.LoadComponent("suite.config.yaml");

            _launcher       = Launcher.Start(cfg.ToAppLauncherConfig());
            TestHttpClient  = TestHttpClient.Create(cfg.Api.Url!, null);
            SuiteConfig     = cfg.ToSuiteConfig();
            SuiteConfig.ApiResponseFolder = EnsureDirectory(cfg.Folders?.Response ?? "responses");
            Filter          = cfg.ToTestFilter();
            ResultCollector = new TestResultCollector();
        }

        public TestHttpClient TestHttpClient { get; }
        public SuiteConfig SuiteConfig { get; }
        public TestFilter Filter { get; }
        public TestResultCollector ResultCollector { get; }

        private static string EnsureDirectory(string relativePath) =>
            Directory.CreateDirectory(
                Path.Combine(Environment.CurrentDirectory, relativePath)).FullName;

        public void Dispose()
        {
            _launcher.Dispose();
            ResultCollector.Dispose();
        }
    }
}

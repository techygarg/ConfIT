using System;
using System.IO;
using ConfIT;
using ConfIT.Config;
using ConfIT.Extension;
using ConfIT.Reporting;
using ConfIT.Runner.Boot;
using ConfIT.Runner.Http;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace User.ComponentTests.Launcher.SetUp
{
    /// <summary>
    /// Demonstrates the AppLauncher (command) mode with OAuth2 client credentials auth.
    ///
    /// Startup order matters:
    ///   1. OAuth2 WireMock (port 8887) starts first — stub must exist before the provider
    ///      fetches the token in step 4.
    ///   2. AppLauncher starts User.Api (port 5170).
    ///   3. suite.config.yaml is loaded.
    ///   4. cfg.ToAuthTokenProvider() constructs OAuth2ClientCredentialsProvider, which
    ///      POSTs to localhost:8887/oauth/token and caches the access_token.
    ///   5. TestHttpClient is created with the resolved provider — every request carries
    ///      Authorization: Bearer component-test-token.
    ///
    /// The JustAnotherService mock (port 8888) is still managed per-test by ConfIT as before.
    /// </summary>
    public class TestSuiteFixture : IDisposable
    {
        private readonly AppLauncher       _launcher;
        private readonly WireMockServer    _oauthServer;

        public TestSuiteFixture()
        {
            // Step 1 — OAuth2 token endpoint stub must be ready before the provider is built.
            _oauthServer = WireMockServer.Start(8887);
            _oauthServer
                .Given(Request.Create().WithPath("/oauth/token").UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody("""{"access_token":"component-test-token","token_type":"Bearer"}""")
                    .WithHeader("Content-Type", "application/json"));

            var cfg = SuiteConfiguration.LoadComponent("suite.config.yaml");

            // Step 2 — start User.Api as an external process.
            _launcher = AppLauncher.Start(cfg.ToAppLauncherConfig());

            // Step 4 — provider construction POSTs to port 8887 and caches the token.
            var authProvider = cfg.ToAuthTokenProvider()
                ?? throw new InvalidOperationException("No auth block found in suite.config.yaml.");
            TestHttpClient  = TestHttpClient.Create(cfg.Api.Url!, authProvider);
            SuiteConfig     = cfg.ToSuiteConfig();
            SuiteConfig.ApiResponseFolder = EnsureDirectory(cfg.Folders?.Response ?? "responses");
            Filter          = cfg.ToTestFilter();
            ResultCollector = new TestResultCollector();
        }

        public TestHttpClient      TestHttpClient  { get; }
        public SuiteConfig         SuiteConfig     { get; }
        public TestFilter?         Filter          { get; }
        public TestResultCollector ResultCollector { get; }

        private static string EnsureDirectory(string relativePath) =>
            Directory.CreateDirectory(
                Path.Combine(Environment.CurrentDirectory, relativePath)).FullName;

        public void Dispose()
        {
            _launcher.Dispose();
            _oauthServer.Stop();
            ResultCollector.Dispose();
        }
    }
}

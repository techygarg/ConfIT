using System;
using ConfIT;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace User.ComponentTests.AppLauncher.SetUp
{
    /// <summary>
    /// Demonstrates the AppLauncher (command) mode with OAuth2 client credentials auth.
    ///
    /// Startup order matters:
    ///   1. OAuth2 WireMock (port 8887) starts first — the stub must exist before
    ///      SuiteBootstrapper.ForCommand() constructs the auth provider, which eagerly
    ///      POSTs to the token endpoint.
    ///   2. ForCommand() reads suite.config.yaml, starts User.Api via AppLauncher, then
    ///      builds OAuth2ClientCredentialsProvider (which calls port 8887) and wires
    ///      everything else.
    ///
    /// The JustAnotherService mock (port 8888) is still managed per-test by ConfIT.
    /// </summary>
    public class TestSuiteFixture : IDisposable
    {
        private readonly BootstrappedSuite _suite;
        private readonly WireMockServer    _oauthServer;

        public TestSuiteFixture()
        {
            // Step 1 — OAuth2 token stub must be ready before ForCommand() is called.
            _oauthServer = WireMockServer.Start(8887);
            _oauthServer
                .Given(Request.Create().WithPath("/oauth/token").UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithBody("""{"access_token":"component-test-token","token_type":"Bearer"}""")
                    .WithHeader("Content-Type", "application/json"));

            // Step 2 — bootstrapper starts the process and builds auth provider.
            _suite = SuiteBootstrapper.ForCommand("suite.config.yaml");
        }

        public TestSuiteContext Context => _suite.Context;

        public void Dispose()
        {
            _suite.Dispose();
            _oauthServer.Stop();
        }
    }
}

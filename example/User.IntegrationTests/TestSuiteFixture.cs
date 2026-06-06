using System;
using ConfIT;

namespace User.IntegrationTests
{
    public class TestSuiteFixture : IDisposable
    {
        private readonly BootstrappedSuite _suite;

        public TestSuiteFixture() =>
            _suite = SuiteBootstrapper.ForIntegration("suite.config.yaml");

        public TestSuiteContext Context => _suite.Context;

        public void Dispose() => _suite.Dispose();
    }
}

using System;
using System.IO;
using ConfIT;
using ConfIT.Config;
using ConfIT.Extension;
using ConfIT.Server.Http;

namespace User.IntegrationTests
{
    public class TestSuiteFixture : IDisposable
    {
        public TestSuiteFixture()
        {
            var cfg = SuiteConfiguration.LoadIntegration("suite.config.yaml");
            SuiteConfig    = cfg.ToSuiteConfig();
            TestHttpClient = TestHttpClient.Create(cfg.Api.Url!, new AuthTokenProvider());
            Filter         = cfg.ToTestFilter();
            ResultCollector = new TestResultCollector();
            Directory.CreateDirectory(Environment.CurrentDirectory + $"/{SuiteConfig.ApiResponseFolder}");
        }

        public TestHttpClient TestHttpClient { get; }
        public SuiteConfig SuiteConfig { get; }
        public TestFilter Filter { get; }
        public TestResultCollector ResultCollector { get; }

        public void Dispose() => ResultCollector.Dispose();
    }
}

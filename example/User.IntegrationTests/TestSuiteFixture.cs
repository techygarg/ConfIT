using System;
using System.IO;
using ConfIT;
using ConfIT.Server.Http;

namespace User.IntegrationTests
{
    public class TestSuiteFixture : IDisposable
    {
        public TestSuiteFixture()
        {
            SuiteConfig = new SuiteConfig
            {
                ApiResponseFolder = "ApiResponses",
                RequestBodyFolder = "TestCase/Request",
                ResponseBodyFolder = "TestCase/Response",
                ApiServerUrl = "http://localhost:5170"
            };
            TestHttpClient = TestHttpClient.Create(SuiteConfig.ApiServerUrl, new AuthTokenProvider());
            // Filter by tags via RUN_POOLS env var, or by test names via RUN_TESTS env var
            Filter = TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS");
            // Filter = TestFilter.CreateForTestsFromEnvVariable("RUN_TESTS");
            CreateDirectoryForResponse(SuiteConfig.ApiResponseFolder);
        }

        public TestHttpClient TestHttpClient { get; }
        public SuiteConfig SuiteConfig { get; }
        public TestFilter Filter { get; }
        public TestResultCollector ResultCollector { get; } = new TestResultCollector();

        public void Dispose() => ResultCollector.Dispose();

        private void CreateDirectoryForResponse(string folder) =>
            Directory.CreateDirectory(Environment.CurrentDirectory + $"/{folder}");
    }
}

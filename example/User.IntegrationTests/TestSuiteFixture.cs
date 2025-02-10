using System;
using System.IO;
using ConfIT;
using ConfIT.Contract;
using ConfIT.Server.Http;

namespace User.IntegrationTests
{
    public class TestSuiteFixture
    {
        public TestSuiteFixture()
        {
            Environment.SetEnvironmentVariable("RUN_POOLS", "multilevel");
            Environment.SetEnvironmentVariable("RUN_TESTS", "ShouldCreateAUser,ShouldCreateAUser_v1,ShouldCreateAUser_v2");
            SuiteConfig = new SuiteConfig
            {
                ApiResponseFolder = "ApiResponses",
                RequestBodyFolder = "TestCase/Request",
                ResponseBodyFolder = "TestCase/Response",
                ApiServerUrl = "http://localhost:5170"
            };
            TestHttpClient = TestHttpClient.Create(SuiteConfig.ApiServerUrl, new AuthTokenProvider());
            TestProcessFactory = TestProcessor.TestProcessorFactory.Create(SuiteConfig);
            // if we want to run few tests by names or by tags, if we pass those values in filter
            Filter = TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS");
            // Filter = TestFilter.CreateForTestsFromEnvVariable("RUN_TESTS");
            CreateDirectoryForResponse(SuiteConfig.ApiResponseFolder);
        }

        public TestHttpClient TestHttpClient { get; }
        public SuiteConfig SuiteConfig { get; }
        public ITestProcessorFactory TestProcessFactory { get; }
        public TestFilter Filter { get; }

        private void CreateDirectoryForResponse(string folder) =>
            Directory.CreateDirectory(Environment.CurrentDirectory + $"/{folder}");
    }
}
using System;
using System.IO;
using ConfIT;
using ConfIT.Server.Http;
using Microsoft.Extensions.DependencyInjection;
using User.Api.Persistence;

namespace User.ComponentTests.SetUp
{
    public class TestSuiteFixture : IDisposable
    {
        public TestSuiteFixture()
        {
            var server = InitializeServer();
            InitializeDb(server);
        }

        public TestHttpClient TestHttpClient { get; private set; }
        public SuiteConfig SuiteConfig { get; private set; }
        public TestFilter Filter { get; private set; }
        public TestResultCollector ResultCollector { get; } = new TestResultCollector();

        private TestSuiteInitializer<TestServerStartup> InitializeServer()
        {
            var initializer = new TestSuiteInitializer<TestServerStartup>("appsettings.Tests.json");
            TestHttpClient = initializer.TestHttpClient;
            SuiteConfig = new SuiteConfig
            {
                MockServerUrl = "http://localhost:8888",
                ApiResponseFolder = CreateDirectoryForResponse()
            };
            // Filter by tags via RUN_POOLS env var, or by test names via RUN_TESTS env var
            // Filter = TestFilter.CreateForTagsFromEnvVariable("RUN_POOLS");
            return initializer;
        }

        private static void InitializeDb(TestSuiteInitializer<TestServerStartup> suite)
        {
            var dbContext = suite.TestServer.Services.GetService<UserDbContext>();
            var dbInitializer = new UserDbInitializer(dbContext);
            dbInitializer.Seed();
        }

        private string CreateDirectoryForResponse()
        {
            var directoryInfo = Directory.CreateDirectory(Environment.CurrentDirectory + "/responses");
            return directoryInfo.FullName;
        }

        public void Dispose() => ResultCollector.Dispose();
    }
}

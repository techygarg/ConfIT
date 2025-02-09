using System;
using System.IO;
using ConfIT;
using ConfIT.Contract;
using ConfIT.Server.Http;
using Microsoft.Extensions.DependencyInjection;
using User.Api.Persistence;
using User.ComponentTests.SetUp.TestProcessor;

namespace User.ComponentTests.SetUp
{
    public class TestSuiteFixture
    {
        public TestSuiteFixture()
        {
            var server = InitializeServer();
            InitializeDb(server);
        }

        public TestHttpClient TestHttpClient { get; private set; }
        public SuiteConfig SuiteConfig { get; private set; }
        public ITestProcessorFactory TestProcessFactory { get; private set; }
        public TestFilter Filter { get; private set; }

        private TestSuiteInitializer<TestServerStartup> InitializeServer()
        {
            var initializer = new TestSuiteInitializer<TestServerStartup>("appsettings.Tests.json");
            TestHttpClient = initializer.TestHttpClient;
            SuiteConfig = new SuiteConfig
            {
                MockServerUrl = "http://localhost:8888",
                ApiResponseFolder = CreateDirectoryForResponse()
            };
            TestProcessFactory = new TestProcessFactory();
            // if we want to run few tests by names or by tags, if we pass those values in filter
            // Filter = TestFilter.Create("pool1,user", null);
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
    }
}
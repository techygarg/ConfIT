using System;
using System.IO;
using ConfIT;
using ConfIT.Config;
using ConfIT.Extension;
using ConfIT.Reporting;
using ConfIT.Runner.Http;
using Microsoft.Extensions.DependencyInjection;
using User.Api;
using User.Api.Persistence;

namespace User.ComponentTests.SetUp
{
    public class TestSuiteFixture : IDisposable
    {
        public TestSuiteFixture()
        {
            var cfg = SuiteConfiguration.LoadComponent("suite.config.yaml");
            var initializer = new TestSuiteInitializer<Startup>(cfg.Startup.Settings!);
            InitializeDb(initializer);
            TestHttpClient  = initializer.TestHttpClient;
            SuiteConfig     = cfg.ToSuiteConfig();
            SuiteConfig.ApiResponseFolder = EnsureDirectory(cfg.Folders?.Response ?? "responses");
            Filter          = cfg.ToTestFilter();
            ResultCollector = new TestResultCollector();
        }

        public TestHttpClient TestHttpClient { get; private set; }
        public SuiteConfig SuiteConfig { get; private set; }
        public TestFilter? Filter { get; private set; }
        public TestResultCollector ResultCollector { get; }

        private static void InitializeDb(TestSuiteInitializer<Startup> initializer)
        {
            using var scope = initializer.Services.CreateScope();
            var dbContext     = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var dbInitializer = new UserDbInitializer(dbContext);
            dbInitializer.Seed();
        }

        private static string EnsureDirectory(string relativePath) =>
            Directory.CreateDirectory(
                Path.Combine(Environment.CurrentDirectory, relativePath)).FullName;

        public void Dispose() => ResultCollector.Dispose();
    }
}

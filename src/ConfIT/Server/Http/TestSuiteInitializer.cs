using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ConfIT.Server.Http
{
    public class TestSuiteInitializer<TStartUp> where TStartUp : class, IDisposable
    {
        public TestServer TestServer { get; private set; }
        public TestHttpClient TestHttpClient { get; private set; }

        public TestSuiteInitializer(string appSettingFileName)
        {
            if (string.IsNullOrWhiteSpace(appSettingFileName))
                throw new ArgumentException("Please provide app settings file name");

            var config = ConfigurationRoot(appSettingFileName);
            InitializeHttpServer(config);
            InitializeHttpClient();
        }

        private void InitializeHttpClient() =>
            TestHttpClient = new TestHttpClient(TestServer.CreateClient());

        private void InitializeHttpServer(IConfiguration config)
        {
            var builder = WebHost.CreateDefaultBuilder()
                .UseConfiguration(config)
                .ConfigureLogging(factory =>
                {
                    factory.SetMinimumLevel(LogLevel.Information);
                    factory.AddConsole();
                })
                .UseTestServer()
                .UseStartup<TStartUp>();

            TestServer = new TestServer(builder);
        }

        private static IConfigurationRoot ConfigurationRoot(string appSettingFileName) =>
            new ConfigurationBuilder()
                .AddJsonFile(appSettingFileName)
                .Build();

        public void Dispose()
        {
            TestServer?.Dispose();
            TestHttpClient?.Dispose();
        }
    }
}
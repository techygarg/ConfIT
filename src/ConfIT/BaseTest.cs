using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ConfIT.Contract;
using ConfIT.Extension;
using ConfIT.Server.Dto;
using ConfIT.Server.Http;
using ConfIT.Server.Mock;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using static ConfIT.Util.ResultMatcher;
using static System.IO.Path;

namespace ConfIT
{
    public abstract class BaseTest : IDisposable
    {
        protected readonly TestHttpClient HttpClient;
        protected static SuiteConfig Config;
        protected readonly ITestProcessorFactory Factory;
        protected readonly ITestOutputLogger TestOutputLogger;
        protected readonly TestFilter Filter;
        protected readonly HttpMockServer HttpMockServer;

        protected BaseTest(
            TestHttpClient httpClient,
            SuiteConfig config,
            ITestProcessorFactory factory,
            ITestOutputLogger testOutputLogger,
            TestFilter filter)
        {
            Config = config;
            Factory = factory;
            HttpClient = httpClient;
            TestOutputLogger = testOutputLogger;
            Filter = filter;

            if (!Config.MockServerUrl.IsNullOrWhiteSpace())
                HttpMockServer = new HttpMockServer(Config.MockServerUrl, Config.EnableMockServerLogs);
        }

        protected virtual async Task Execute(string testName, TestCase testCase)
        {
            if (ShouldSkipTheTest(testName, testCase))
            {
                Log($"Skipping Test : {testName}");
                return;
            }
    
            Log("*********************************************");
            Log($"Start Executing testCase {testName}..........");
            HttpMockServer?.Initialize(testCase.Mock);

            var testProcessor = Factory?.GetTestProcessor(testName);
            testProcessor?.Before(testCase.Api);

            var response = await HttpClient.Execute(testCase.Api);
            var actualResponseBody = JToken.Parse(response.Content.ReadAsStringAsync().Result);
            var expectedResponseBodyJToken = testCase.Api.Response.Body;

            Log(actualResponseBody, expectedResponseBodyJToken, testCase);
            testProcessor?.After(testCase.Api, actualResponseBody);
            Verify(response, actualResponseBody, expectedResponseBodyJToken, testCase.Api);
            SaveApiResponse(Config.ApiResponseFolder, testName, actualResponseBody);
            Log("*********************************************");
            Console.WriteLine();
        }

        protected void Log(JToken responseBody, JToken expectedResponseBody, TestCase test)
        {
            Log($"Actual Response Body --> {responseBody}");
            Log($"Expected Response Body --> {expectedResponseBody}");
            Log($"Pattern Matchers --> {test.Api.Response.Matcher?.Pattern?.DictionaryToString()}");
            Log($"Ignore Matchers --> {test.Api.Response.Matcher?.Ignore?.ListToString()}");
            Log($"Tags --> {test.Tags?.ListToString()}");
        }

        protected virtual void Log(string msg)
        {
            TestOutputLogger?.Log(msg);
            Console.WriteLine(msg);
        }

        protected virtual void Verify(HttpResponseMessage response, JToken actualResponseBody, JToken expectedResponseBodyJToken, TestApi testApi)
        {
            response.StatusCode.Should().Be(Enum.Parse<HttpStatusCode>(testApi.Response.StatusCode.ToString()));
            MatchResponseBody(actualResponseBody, expectedResponseBodyJToken, testApi.Response.Matcher);
        }

        protected bool ShouldSkipTheTest(string testName, TestCase testCase)
        {
            if (Filter != null)
            {
                if (Filter.TestNames?.Count > 0
                    && Filter
                        .TestNames
                        .FirstOrDefault(x => x.Trim().Equals(testName.Trim(), StringComparison.InvariantCultureIgnoreCase)) == null)
                    return true;

                if (Filter.Tags?.Count > 0)
                {
                    if (testCase.Tags == null
                        || testCase.Tags.Count == 0
                        || !testCase.Tags.Select(s => s.Trim())
                            .Intersect(Filter.Tags.Select(s => s.Trim()), StringComparer.InvariantCultureIgnoreCase)
                            .Any())
                        return true;
                }
            }

            return false;
        }

        protected virtual void SaveApiResponse(string apiResponseFolder, string testName, JToken response) =>
            File.WriteAllText($"{GetFullPath(apiResponseFolder)}/{testName.ToLower()}.json", response.ToString());

        public virtual void Dispose() =>
            HttpMockServer?.Dispose();
    }
}
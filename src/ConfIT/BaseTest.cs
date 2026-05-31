using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ConfIT.Contract;
using ConfIT.Extension;
using ConfIT.Server.Dto;
using ConfIT.Server.Http;
using ConfIT.Server.Mock;
using ConfIT.Util;
using ConfIT.Variable;
using FluentAssertions;
using static ConfIT.Util.ResultMatcher;
using static System.IO.Path;

namespace ConfIT;

public abstract class BaseTest : IDisposable
{
    private const string Separator = "*********************************************";

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

        Log(Separator);
        Log($"Start Executing testCase {testName}..........");
        var testProcessor = Factory?.GetTestProcessor(testName);
        var resolvedCase = VariableInjector.Inject(testCase, VariableStore.Instance);

        SemanticMatcher.ValidateSpecs(resolvedCase.Api.Response.Matcher?.Semantic, Config.CustomMatchers);

        HttpMockServer?.Initialize(resolvedCase.Mock);
        
        testProcessor?.Before(resolvedCase.Api);

        var response = await HttpClient.Execute(resolvedCase.Api);
        var actualResponseBody = JToken.Parse(response.Content.ReadAsStringAsync().Result);
        var expectedResponseBody = resolvedCase.Api.Response.Body;

        Log(actualResponseBody, expectedResponseBody, resolvedCase);
        testProcessor?.After(resolvedCase.Api, actualResponseBody);
        Verify(response, actualResponseBody, expectedResponseBody, resolvedCase.Api);

        VariableExtractor.Extract(testName, response, actualResponseBody,
            resolvedCase.Api.Response.Extract, VariableStore.Instance);

        SaveApiResponse(Config.ApiResponseFolder, testName, actualResponseBody);
        Log(Separator);
        Console.WriteLine();
    }

    protected void Log(JToken actualBody, JToken expectedBody, TestCase test)
    {
        Log($"Actual Response Body --> {actualBody}");
        Log($"Expected Response Body --> {expectedBody}");
        Log($"Semantic Matchers --> {test.Api.Response.Matcher?.Semantic?.DictionaryToString()}");
        Log($"Pattern Matchers --> {test.Api.Response.Matcher?.Pattern?.DictionaryToString()}");
        Log($"Ignore Matchers --> {test.Api.Response.Matcher?.Ignore?.ListToString()}");
        Log($"Tags --> {test.Tags?.ListToString()}");
    }

    protected virtual void Log(string msg)
    {
        TestOutputLogger?.Log(msg);
        Console.WriteLine(msg);
    }

    protected virtual void Verify(
        HttpResponseMessage response,
        JToken actualResponseBody,
        JToken expectedResponseBody,
        TestApi testApi)
    {
        response.StatusCode.Should().Be(Enum.Parse<HttpStatusCode>(testApi.Response.StatusCode.ToString()));
        MatchResponseBody(actualResponseBody, expectedResponseBody, testApi.Response.Matcher, Config.CustomMatchers);
    }

    protected bool ShouldSkipTheTest(string testName, TestCase testCase)
    {
        if (Filter is null) return false;

        if (Filter.TestNames?.Count > 0
            && !Filter.TestNames.Any(n => n.Trim().Equals(testName.Trim(), StringComparison.InvariantCultureIgnoreCase)))
            return true;

        if (Filter.Tags?.Count > 0)
        {
            if (testCase.Tags is not { Count: > 0 }
                || !testCase.Tags.Select(s => s.Trim())
                    .Intersect(Filter.Tags.Select(s => s.Trim()), StringComparer.InvariantCultureIgnoreCase)
                    .Any())
                return true;
        }

        return false;
    }

    protected virtual void SaveApiResponse(string apiResponseFolder, string testName, JToken response) =>
        File.WriteAllText($"{GetFullPath(apiResponseFolder)}/{testName.ToLower()}.json", response.ToString());

    public virtual void Dispose() =>
        HttpMockServer?.Dispose();
}

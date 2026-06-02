using System.Diagnostics;
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
    private const string HeaderSep = "══════════════════════════════════════════════════════";
    private const string FooterSep = "──────────────────────────────────────────────────────";
    protected static SuiteConfig Config;

    // Console is the single channel for structured test output (header, bodies, matchers).
    // ITestOutputHelper is intentionally NOT used for structured content — both xUnit's
    // failure reporter and MSBuild's error reporter replay it, causing visible duplication.
    // Output is buffered per-test and flushed atomically in Execute's finally block so
    // one test's console block never interleaves with the next test's.
    private readonly List<string> _consoleBuffer = new();
    private readonly TestResultCollector? _resultCollector;
    protected readonly ITestProcessorFactory Factory;
    protected readonly TestFilter Filter;

    protected readonly TestHttpClient HttpClient;
    protected readonly HttpMockServer HttpMockServer;
    protected readonly ITestOutputLogger TestOutputLogger;

    protected BaseTest(
        TestHttpClient httpClient,
        SuiteConfig config,
        ITestProcessorFactory factory,
        ITestOutputLogger testOutputLogger,
        TestFilter filter,
        TestResultCollector? resultCollector = null)
    {
        Config = config;
        Factory = factory;
        HttpClient = httpClient;
        TestOutputLogger = testOutputLogger;
        Filter = filter;
        _resultCollector = resultCollector;

        if (!string.IsNullOrWhiteSpace(Config.MockServerUrl))
            HttpMockServer = new HttpMockServer(Config.MockServerUrl, Config.EnableMockServerLogs);
    }

    public virtual void Dispose()
    {
        HttpMockServer?.Dispose();
    }

    protected virtual async Task Execute(string testName, TestCase testCase, string? sourceFile = null)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (ShouldSkipTheTest(testName, testCase))
            {
                TestOutputLogger?.Log($"  ⏭  Skipping: {testName}");
                _consoleBuffer.Add(TestColor.Subtle($"  ⏭  Skipping: {testName}"));
                _resultCollector?.Record(testName, TestResultCollector.TestStatus.Skipped, sourceFile: sourceFile);
                return;
            }

            LogHeader(testName);
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
            _consoleBuffer.Add(TestColor.Subtle(FooterSep));
            _consoleBuffer.Add(string.Empty);
            _resultCollector?.Record(testName, TestResultCollector.TestStatus.Passed, sw.Elapsed, sourceFile);
        }
        catch (Exception)
        {
            _resultCollector?.Record(testName, TestResultCollector.TestStatus.Failed, sw.Elapsed, sourceFile);
            throw;
        }
        finally
        {
            sw.Stop();
            foreach (var line in _consoleBuffer)
                Console.WriteLine(line);
            _consoleBuffer.Clear();
        }
    }

    protected void Log(JToken actualBody, JToken expectedBody, TestCase test)
    {
        var matcher = test.Api.Response.Matcher;

        _consoleBuffer.Add($"{TestColor.Info("Actual:")}   {actualBody}");
        _consoleBuffer.Add(string.Empty);
        _consoleBuffer.Add($"{TestColor.Expected("Expected:")} {expectedBody}");
        if (matcher?.Semantic?.Count > 0)
            _consoleBuffer.Add(TestColor.Subtle($"Semantic: {matcher.Semantic.DictionaryToString()}"));
        if (matcher?.Pattern?.Count > 0)
            _consoleBuffer.Add(TestColor.Subtle($"Pattern:  {matcher.Pattern.DictionaryToString()}"));
        if (matcher?.Ignore?.Count > 0)
            _consoleBuffer.Add(TestColor.Subtle($"Ignore:   {matcher.Ignore.ListToString()}"));
        if (test.Tags?.Count > 0) _consoleBuffer.Add(TestColor.Subtle($"Tags:     {test.Tags.ListToString()}"));
        _consoleBuffer.Add(string.Empty);
    }

    private void LogHeader(string testName)
    {
        _consoleBuffer.Add(TestColor.Structure(HeaderSep));
        _consoleBuffer.Add($"  {TestColor.Structure("▶")}  {TestColor.Emphasis(testName)}");
        _consoleBuffer.Add(TestColor.Structure(HeaderSep));
        _consoleBuffer.Add(string.Empty);
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
            && !Filter.TestNames.Any(n =>
                n.Trim().Equals(testName.Trim(), StringComparison.InvariantCultureIgnoreCase)))
            return true;

        if (Filter.Tags?.Count > 0)
            if (testCase.Tags is not { Count: > 0 }
                || !testCase.Tags.Select(s => s.Trim())
                    .Intersect(Filter.Tags.Select(s => s.Trim()), StringComparer.InvariantCultureIgnoreCase)
                    .Any())
                return true;

        return false;
    }

    protected virtual void SaveApiResponse(string apiResponseFolder, string testName, JToken response)
    {
        File.WriteAllText($"{GetFullPath(apiResponseFolder)}/{testName.ToLower()}.json", response.ToString());
    }
}
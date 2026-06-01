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

    private static readonly bool UseColor =
        !Console.IsOutputRedirected &&
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    private static string Dim(string s)    => UseColor ? $"\x1b[90m{s}\x1b[0m" : s;
    private static string Bold(string s)   => UseColor ? $"\x1b[1m{s}\x1b[0m" : s;
    private static string Yellow(string s) => UseColor ? $"\x1b[33m{s}\x1b[0m" : s;
    private static string Green(string s)  => UseColor ? $"\x1b[32m{s}\x1b[0m" : s;
    private static string Cyan(string s)   => UseColor ? $"\x1b[36m{s}\x1b[0m" : s;

    // Console output is buffered per-test and flushed atomically in Execute's finally block.
    // This prevents interleaving with the next test when xUnit flushes a failed test's
    // ITestOutputHelper buffer after the next test has already started writing to Console.
    private readonly List<string> _consoleBuffer = new();

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

        if (!string.IsNullOrWhiteSpace(Config.MockServerUrl))
            HttpMockServer = new HttpMockServer(Config.MockServerUrl, Config.EnableMockServerLogs);
    }

    protected virtual async Task Execute(string testName, TestCase testCase)
    {
        try
        {
            if (ShouldSkipTheTest(testName, testCase))
            {
                TestOutputLogger?.Log($"  ⏭  Skipping: {testName}");
                _consoleBuffer.Add(Dim($"  ⏭  Skipping: {testName}"));
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
            TestOutputLogger?.Log(FooterSep);
            _consoleBuffer.Add(Dim(FooterSep));
            _consoleBuffer.Add(string.Empty);
        }
        finally
        {
            foreach (var line in _consoleBuffer)
                Console.WriteLine(line);
            _consoleBuffer.Clear();
        }
    }

    protected void Log(JToken actualBody, JToken expectedBody, TestCase test)
    {
        var matcher = test.Api.Response.Matcher;

        TestOutputLogger?.Log($"Actual:   {actualBody}");
        TestOutputLogger?.Log($"Expected: {expectedBody}");
        if (matcher?.Semantic?.Count > 0) TestOutputLogger?.Log($"Semantic: {matcher.Semantic.DictionaryToString()}");
        if (matcher?.Pattern?.Count  > 0) TestOutputLogger?.Log($"Pattern:  {matcher.Pattern.DictionaryToString()}");
        if (matcher?.Ignore?.Count   > 0) TestOutputLogger?.Log($"Ignore:   {matcher.Ignore.ListToString()}");
        if (test.Tags?.Count         > 0) TestOutputLogger?.Log($"Tags:     {test.Tags.ListToString()}");

        _consoleBuffer.Add($"{Yellow("Actual:")}   {actualBody}");
        _consoleBuffer.Add(string.Empty);
        _consoleBuffer.Add($"{Green("Expected:")} {expectedBody}");
        if (matcher?.Semantic?.Count > 0) _consoleBuffer.Add(Dim($"Semantic: {matcher.Semantic.DictionaryToString()}"));
        if (matcher?.Pattern?.Count  > 0) _consoleBuffer.Add(Dim($"Pattern:  {matcher.Pattern.DictionaryToString()}"));
        if (matcher?.Ignore?.Count   > 0) _consoleBuffer.Add(Dim($"Ignore:   {matcher.Ignore.ListToString()}"));
        if (test.Tags?.Count         > 0) _consoleBuffer.Add(Dim($"Tags:     {test.Tags.ListToString()}"));
        _consoleBuffer.Add(string.Empty);
    }

    private void LogHeader(string testName)
    {
        TestOutputLogger?.Log(HeaderSep);
        TestOutputLogger?.Log($"  ▶  {testName}");
        TestOutputLogger?.Log(HeaderSep);
        _consoleBuffer.Add(Cyan(HeaderSep));
        _consoleBuffer.Add($"  {Cyan("▶")}  {Bold(testName)}");
        _consoleBuffer.Add(Cyan(HeaderSep));
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

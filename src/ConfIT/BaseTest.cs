using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ConfIT.Config;
using ConfIT.Contract;
using ConfIT.Extension;
using ConfIT.Matching;
using ConfIT.Model;
using ConfIT.Reporting;
using ConfIT.Runner.Http;
using ConfIT.Runner.Mock;
using ConfIT.Variable;
using FluentAssertions;
using static ConfIT.Matching.ResultMatcher;
using static System.IO.Path;

namespace ConfIT;

public abstract class BaseTest : IDisposable
{
    private const string HeaderSep = "══════════════════════════════════════════════════════";
    private const string FooterSep = "──────────────────────────────────────────────────────";

    // Console is the single channel for structured test output (header, bodies, matchers).
    // ITestOutputHelper is intentionally NOT used for structured content — both xUnit's
    // failure reporter and MSBuild's error reporter replay it, causing visible duplication.
    // Output is buffered per-test and flushed atomically in Execute's finally block so
    // one test's console block never interleaves with the next test's.
    private readonly List<string>         _consoleBuffer = new();
    private readonly TestResultCollector? _resultCollector;
    private readonly SuiteConfig          _config;
    protected readonly ITestProcessorFactory? Factory;
    protected readonly TestFilter?            Filter;
    protected readonly TestHttpClient         HttpClient;
    protected readonly HttpMockServer?        HttpMockServer;
    protected readonly ITestOutputLogger?     TestOutputLogger;

    // Convenience accessor — preserved for subclass compatibility
    protected SuiteConfig Config => _config;

    protected BaseTest(TestSuiteContext context, ITestOutputLogger? logger = null)
    {
        _config          = context.Config;
        Factory          = context.ProcessorFactory;
        HttpClient       = context.HttpClient;
        TestOutputLogger = logger;
        Filter           = context.Filter;
        _resultCollector = context.ResultCollector;

        if (!string.IsNullOrWhiteSpace(_config.MockServerUrl))
            HttpMockServer = new HttpMockServer(_config.MockServerUrl, _config.EnableMockServerLogs);
    }

    // Legacy 6-param constructor — kept for backward compatibility with existing fixtures
    protected BaseTest(
        TestHttpClient            httpClient,
        SuiteConfig               config,
        ITestProcessorFactory?    factory,
        ITestOutputLogger?        testOutputLogger,
        TestFilter?               filter,
        TestResultCollector?      resultCollector = null)
        : this(new TestSuiteContext(httpClient, config, factory, filter, resultCollector), testOutputLogger)
    {
    }

    public virtual void Dispose()
    {
        HttpMockServer?.Dispose();
    }

    #region Execute pipeline

    protected virtual async Task Execute(string testName, JToken test, string? sourceFile = null)
    {
        var testCase = test.ToTestCase(_config.RequestBodyFolder, _config.ResponseBodyFolder);
        await Execute(testName, testCase, sourceFile);
    }

    protected virtual async Task Execute(string testName, TestCase testCase, string? sourceFile = null)
    {
        if (TrySkipForDependency(testName, testCase, sourceFile)) return;
        if (TrySkipForFilter(testName, testCase, sourceFile)) return;
        await RunTest(testName, testCase, sourceFile);
    }

    private bool TrySkipForDependency(string testName, TestCase testCase, string? sourceFile)
    {
        var blocked = TestDependencyStore.Instance.CheckPrerequisites(testCase.Depends);
        if (blocked is null) return false;

        var reason = blocked.Value.Status == TestRunStatus.Failed
            ? $"prerequisite '{blocked.Value.Name}' failed"
            : $"prerequisite '{blocked.Value.Name}' was skipped";
        SkipTest(testName, sourceFile, reason);
        return true;
    }

    private bool TrySkipForFilter(string testName, TestCase testCase, string? sourceFile)
    {
        if (!ShouldSkipTheTest(testName, testCase)) return false;
        SkipTest(testName, sourceFile);
        return true;
    }

    private void SkipTest(string testName, string? sourceFile, string? reason = null)
    {
        var message = reason is not null
            ? $"  ⏭  Skipping: {testName} ({reason})"
            : $"  ⏭  Skipping: {testName}";
        TestOutputLogger?.Log(message);
        _consoleBuffer.Add(TestColor.Subtle(message));
        RecordStatus(testName, TestRunStatus.Skipped, sourceFile: sourceFile, reason: reason);
    }

    private async Task RunTest(string testName, TestCase testCase, string? sourceFile)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            LogHeader(testName);

            var testProcessor = Factory?.GetTestProcessor(testName);
            var resolvedCase  = VariableInjector.Inject(testCase, VariableStore.Instance);

            SemanticMatcher.ValidateSpecs(resolvedCase.Api.Response.Matcher?.Semantic, _config.CustomMatchers);
            HttpMockServer?.Initialize(resolvedCase.Mock);
            testProcessor?.Before(resolvedCase.Api);

            var response   = await HttpClient.Execute(resolvedCase.Api);
            var content    = await response.Content.ReadAsStringAsync();
            var actualBody = JToken.Parse(content);
            var expectedBody = resolvedCase.Api.Response.Body;

            Log(actualBody, expectedBody, resolvedCase);
            testProcessor?.After(resolvedCase.Api, actualBody);
            Verify(response, actualBody, expectedBody, resolvedCase.Api);

            VariableExtractor.Extract(testName, response, actualBody,
                resolvedCase.Api.Response.Extract, VariableStore.Instance);
            SaveApiResponse(_config.ApiResponseFolder, testName, actualBody);

            _consoleBuffer.Add(TestColor.Subtle(FooterSep));
            _consoleBuffer.Add(string.Empty);
            RecordStatus(testName, TestRunStatus.Passed, sw.Elapsed, sourceFile);
        }
        catch (Exception)
        {
            RecordStatus(testName, TestRunStatus.Failed, sw.Elapsed, sourceFile);
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

    private void RecordStatus(string testName, TestRunStatus status, TimeSpan? duration = null,
        string? sourceFile = null, string? reason = null)
    {
        TestDependencyStore.Instance.RecordStatus(testName, status);
        _resultCollector?.Record(testName, status, duration, sourceFile, reason);
    }

    #endregion

    #region Response validation

    protected virtual void Verify(
        HttpResponseMessage response,
        JToken actualResponseBody,
        JToken expectedResponseBody,
        TestApi testApi)
    {
        response.StatusCode.Should().Be(Enum.Parse<HttpStatusCode>(testApi.Response.StatusCode.ToString()));
        var result = MatchResponseBody(actualResponseBody, expectedResponseBody,
            testApi.Response.Matcher, _config.CustomMatchers);
        if (!result.Passed)
            false.Should().BeTrue(result.Description);
    }

    protected bool ShouldSkipTheTest(string testName, TestCase testCase)
    {
        if (Filter is null) return false;

        if (Filter.TestNames.Count > 0
            && !Filter.TestNames.Any(n =>
                n.Trim().Equals(testName.Trim(), StringComparison.InvariantCultureIgnoreCase)))
            return true;

        if (Filter.Tags.Count > 0)
            if (testCase.Tags is not { Count: > 0 }
                || !testCase.Tags.Select(s => s.Trim())
                    .Intersect(Filter.Tags.Select(s => s.Trim()), StringComparer.InvariantCultureIgnoreCase)
                    .Any())
                return true;

        return false;
    }

    #endregion

    #region Output

    protected void Log(JToken actualBody, JToken? expectedBody, TestCase test)
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
        if (test.Tags?.Count > 0)
            _consoleBuffer.Add(TestColor.Subtle($"Tags:     {test.Tags.ListToString()}"));
        _consoleBuffer.Add(string.Empty);
    }

    protected virtual void Log(string msg)
    {
        TestOutputLogger?.Log(msg);
        Console.WriteLine(msg);
    }

    private void LogHeader(string testName)
    {
        _consoleBuffer.Add(TestColor.Structure(HeaderSep));
        _consoleBuffer.Add($"  {TestColor.Structure("▶")}  {TestColor.Emphasis(testName)}");
        _consoleBuffer.Add(TestColor.Structure(HeaderSep));
        _consoleBuffer.Add(string.Empty);
    }

    #endregion

    #region Persistence

    protected virtual void SaveApiResponse(string apiResponseFolder, string testName, JToken response)
    {
        if (string.IsNullOrWhiteSpace(apiResponseFolder)) return;
        File.WriteAllText($"{GetFullPath(apiResponseFolder)}/{testName.ToLower()}.json", response.ToString());
    }

    #endregion
}

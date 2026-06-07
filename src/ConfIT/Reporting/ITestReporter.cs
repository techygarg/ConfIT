namespace ConfIT.Reporting;

// Extensibility point for custom reporting (graphical output, CI integrations, OpenAPI).
// Not yet wired into the pipeline — TestResultCollector remains the active implementation.
internal interface ITestReporter
{
    void TestStarted(string name);
    void TestPassed(string name, TimeSpan duration);
    void TestFailed(string name, TimeSpan duration);
    void TestSkipped(string name, string? reason);
    void SuiteCompleted();
}

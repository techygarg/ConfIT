using ConfIT.Config;
using ConfIT.Contract;
using ConfIT.Reporting;
using ConfIT.Runner.Http;

namespace ConfIT;

public sealed record TestSuiteContext(
    TestHttpClient          HttpClient,
    SuiteConfig             Config,
    ITestProcessorFactory?  ProcessorFactory  = null,
    TestFilter?             Filter            = null,
    TestResultCollector?    ResultCollector   = null);

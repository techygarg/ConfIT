using ConfIT.Contract;
using Xunit.Abstractions;

namespace User.ComponentTests.SetUp
{
    public class TestOutputLogger : ITestOutputLogger
    {
        private readonly ITestOutputHelper _output;

        public TestOutputLogger(ITestOutputHelper output) =>
            _output = output;

        public void Log(string msg) =>
            _output.WriteLine(msg);
    }
}
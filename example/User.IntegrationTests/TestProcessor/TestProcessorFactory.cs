using ConfIT;
using ConfIT.Contract;

namespace User.IntegrationTests.TestProcessor
{
    /// <summary>
    /// If required, we can add our custom per testApi based before and after actions, so that consumer can extend default functionality.
    /// This class is initialized in TestSuiteFixture, so that it can pass to BaseComponentTests
    /// This class is created for demo purpose and not required for current tests 
    /// </summary>
    public class TestProcessorFactory : ITestProcessorFactory
    {
        private readonly SuiteConfig _config;

        public TestProcessorFactory(SuiteConfig config)
        {
            _config = config;
        }

        public ITestProcessor? GetTestProcessor(string testName)
        {
            return testName switch
            {
                "ShouldReturnUserForGivenId_V1" => new ShouldReturnUserForGivenIdTestProcessor(_config),
                "ShouldReturnUserForGivenId_V2" => new ShouldReturnUserForGivenIdTestProcessor(_config),
                _ => null
            };
        }

        public static TestProcessorFactory Create(SuiteConfig config)
        {
            return new TestProcessorFactory(config);
        }
    }
}
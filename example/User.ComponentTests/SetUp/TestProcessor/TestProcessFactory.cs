using ConfIT.Contract;

namespace User.ComponentTests.SetUp.TestProcessor
{
    /// <summary>
    /// If required, we can add our custom per testApi based before and after actions, so that consumer can extend default functionality.
    /// This class is initialized in TestSuiteFixture, so that it can pass to BaseComponentTests
    /// This class is created for demo purpose and not required for current tests 
    /// </summary>
    public class TestProcessFactory : ITestProcessorFactory
    {
        public ITestProcessor? GetTestProcessor(string testName)
        {
            return testName switch
            {
                "ShouldCreteAUser" => new ShouldCreteAUserTestProcessor(),
                _ => null
            };
        }
    }
}
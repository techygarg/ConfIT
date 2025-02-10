using ConfIT;
using ConfIT.Contract;
using ConfIT.Extension;
using ConfIT.Server.Dto;
using Newtonsoft.Json.Linq;

namespace User.IntegrationTests.TestProcessor
{
    /// <summary>
    /// We can implement ITestProcessor, which will be created by TestProcessorFactory for matching testApi name.
    /// We can perform required before and after setup. cleanup tasks.
    /// May be  some database state changes, request, response manipulations for dynamic data etc....
    /// </summary>
    public class ShouldReturnUserForGivenIdTestProcessor : ITestProcessor
    {
        private readonly SuiteConfig _config;
        private string _dependentTestName = "ShouldCreateAUser";

        public ShouldReturnUserForGivenIdTestProcessor(SuiteConfig config) =>
            _config = config;

        public void Before(TestApi testApi)
        {
            var json = _dependentTestName.ReadJsonResponse(_config.ApiResponseFolder);
            var userId = json["id"];
            testApi.Request.Path = $"/api/user/{userId}";
        }

        public void After(TestApi testApi, JToken actualResponse)
        {
        }
    }
}
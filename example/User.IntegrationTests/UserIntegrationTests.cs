using System.Collections.Generic;
using System.Threading.Tasks;
using ConfIT;
using ConfIT.Extension;
using ConfIT.Reader;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace User.IntegrationTests
{
    public class UserIntegrationTests : BaseTest, IClassFixture<TestSuiteFixture>
    {
        public UserIntegrationTests(TestSuiteFixture fixture, ITestOutputHelper output)
            : base(fixture.TestHttpClient,
                fixture.SuiteConfig,
                null,
                new TestOutputLogger(output),
                fixture.Filter,
                fixture.ResultCollector)
        {
        }

        [Theory]
        [MemberData(nameof(GetTestCasesForFolder), "TestCase")]
        public async Task ExecuteTest(string testName, JContainer test, string sourceFile) =>
            await Execute(testName, test.ToTestCase(Config.RequestBodyFolder, Config.ResponseBodyFolder), sourceFile);


        /// <summary>
        /// Use this to read tests from a single file
        /// </summary>
        public static IEnumerable<object[]> GetTestCases(string fileName) =>
            TestReader.GetTestsForAFile(fileName, "TestCase");

        /// <summary>
        /// Use this to read tests from a folder
        /// </summary>
        public static IEnumerable<object[]> GetTestCasesForFolder(string folder) =>
            TestReader.GetTestsForAFolder(folder);
    }
}

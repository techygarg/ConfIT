using System.Collections.Generic;
using System.Threading.Tasks;
using ConfIT;
using ConfIT.Extension;
using ConfIT.Reader;
using Newtonsoft.Json.Linq;
using User.ComponentTests.SetUp;
using Xunit;
using Xunit.Abstractions;

namespace User.ComponentTests
{
    public class UserComponentTests : BaseTest, IClassFixture<TestSuiteFixture>
    {
        public UserComponentTests(TestSuiteFixture fixture, ITestOutputHelper output)
            : base(
                fixture.TestHttpClient,
                fixture.SuiteConfig,
                null,
                new TestOutputLogger(output),
                fixture.Filter,
                fixture.ResultCollector)
        {
        }

        /// <summary>
        /// Entry point for testApi. We can run testApi in two ways
        /// TO run by files, we can add
        /// --> [MemberData(nameof(GetTestCases), "user.json")]
        /// --> [MemberData(nameof(GetTestCases), "errors.json")]
        ///  TO run tests by folder, we can add
        /// --> [MemberData(nameof(GetTestCasesForFolder))]
        /// </summary>
        [Theory]
        [MemberData(nameof(GetTestCasesForFolder), "TestCase")]
        public async Task ExecuteTest(string testName, JToken test, string sourceFile) =>
            await Execute(testName, test.ToTestCase(null, null), sourceFile);


        /// <summary>
        /// Use this to read tests from a single file
        /// </summary>
        public static IEnumerable<object[]> GetTestCases(string fileName) =>
            TestReader.GetTestsForAFile("TestCase", fileName);

        /// <summary>
        /// Use this to read tests from a folder
        /// </summary>
        public static IEnumerable<object[]> GetTestCasesForFolder(string folder) =>
            TestReader.GetTestsForAFolder(folder);
    }
}

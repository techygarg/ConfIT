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
            : base(fixture.Context, new TestOutputLogger(output))
        {
        }

        [Theory]
        [MemberData(nameof(GetTestCasesForFolder), "TestCase")]
        public async Task ExecuteTest(string testName, JToken test, string sourceFile) =>
            await Execute(testName, test, sourceFile);

        public static IEnumerable<object[]> GetTestCases(string fileName) =>
            TestReader.GetTestsForAFile(fileName, "TestCase");

        public static IEnumerable<object[]> GetTestCasesForFolder(string folder) =>
            TestReader.GetTestsForAFolder(folder);
    }
}

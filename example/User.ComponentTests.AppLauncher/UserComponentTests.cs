using System.Collections.Generic;
using System.Threading.Tasks;
using ConfIT;
using ConfIT.Extension;
using ConfIT.Reader;
using Newtonsoft.Json.Linq;
using User.ComponentTests.Launcher.SetUp;
using Xunit;
using Xunit.Abstractions;

namespace User.ComponentTests.Launcher
{
    public class UserComponentTests : BaseTest, IClassFixture<TestSuiteFixture>
    {
        public UserComponentTests(TestSuiteFixture fixture, ITestOutputHelper output)
            : base(fixture.Context, null)
        {
        }

        [Theory]
        [MemberData(nameof(GetTestCasesForFolder), "TestCase")]
        public async Task ExecuteTest(string testName, JToken test, string sourceFile) =>
            await Execute(testName, test, sourceFile);

        public static IEnumerable<object[]> GetTestCases(string fileName) =>
            TestReader.GetTestsForAFile("TestCase", fileName);

        public static IEnumerable<object[]> GetTestCasesForFolder(string folder) =>
            TestReader.GetTestsForAFolder(folder);
    }
}

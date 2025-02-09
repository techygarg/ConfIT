using System.Text;
using ConfIT.Util;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ConfIT.UnitTest.Util
{
    public class TestReaderTests
    {
        private const string TestFolder = "TestData";
        private readonly string _testFolderPath;

        public TestReaderTests()
        {
            _testFolderPath = Path.Combine(Directory.GetCurrentDirectory(), TestFolder);
            if (Directory.Exists(_testFolderPath))
            {
                Directory.Delete(_testFolderPath, true);
            }
            Directory.CreateDirectory(_testFolderPath);
        }

        [Fact]
        public void GetTestsForFile_WithValidJson_ReturnsTestCases()
        {
            // Given
            var fileName = "valid.json";
            File.WriteAllText(Path.Combine(TestFolder, fileName), @"{'test1':{'key':'value'}}");

            // When
            var result = TestReader.GetTestsForAFile(TestFolder, fileName).ToList();

            // Then
            result.Should().HaveCount(1);
            result[0][0].Should().Be("test1");
            result[0][1].Should().NotBeNull();
        }

        [Fact]
        public void GetTestsForFile_ReturnsNameValuePairs()
        {
            // Given
            var fileName = "pairs.json";
            File.WriteAllText(Path.Combine(TestFolder, fileName), @"{'test1':{'prop':'val'},'test2':{'prop2':'val2'}}");

            // When
            var result = TestReader.GetTestsForAFile(TestFolder, fileName).ToList();

            // Then
            result.Should().HaveCount(2);
            result[0][0].Should().Be("test1");
            result[1][0].Should().Be("test2");
        }

        [Fact]
        public void GetTestsForFolder_CombinesMultipleFiles()
        {
            // Given
            File.WriteAllText(Path.Combine(_testFolderPath, "file1.json"), @"{'test1':{'key':'value'}}");
            File.WriteAllText(Path.Combine(_testFolderPath, "file2.json"), @"{'test2':{'key':'value'}}");
            // Add a non-JSON file that should be ignored
            File.WriteAllText(Path.Combine(_testFolderPath, "ignore.txt"), "not json");

            // When
            var result = TestReader.GetTestsForAFolder(TestFolder).ToList();

            // Then
            result.Should().HaveCount(2);
            result.Should().Contain(x => x[0].ToString() == "test1");
            result.Should().Contain(x => x[0].ToString() == "test2");
        }

        [Fact]
        public void GetTestsForFile_HandlesNestedJsonProperties()
        {
            // Given
            var fileName = "nested.json";
            File.WriteAllText(Path.Combine(TestFolder, fileName), @"{'test1':{'nested':{'deep':{'value':123}}}}");

            // When
            var result = TestReader.GetTestsForAFile(TestFolder, fileName).ToList();

            // Then
            result.Should().HaveCount(1);
            result[0][1].Should().BeAssignableTo<JToken>();
        }

        [Fact]
        public void GetTestsForFile_ReturnsEmptyForNoTestCases()
        {
            // Given
            var fileName = "empty.json";
            File.WriteAllText(Path.Combine(TestFolder, fileName), "{}");

            // When
            var result = TestReader.GetTestsForAFile(TestFolder, fileName).ToList();

            // Then
            result.Should().BeEmpty();
        }

        [Fact]
        public void GetTestsForFile_HandlesEmptyJsonFile()
        {
            // Given
            var fileName = "empty.json";
            File.WriteAllText(Path.Combine(TestFolder, fileName), "");

            // When/Then
            Action act = () => TestReader.GetTestsForAFile(TestFolder, fileName).ToList();
            act.Should().Throw<Newtonsoft.Json.JsonReaderException>();
        }

        [Fact]
        public void GetTestsForFile_HandlesInvalidJsonFormat()
        {
            // Given
            var fileName = "invalid.json";
            File.WriteAllText(Path.Combine(TestFolder, fileName), "{invalid json}");

            // When/Then
            Action act = () => TestReader.GetTestsForAFile(TestFolder, fileName).ToList();
            act.Should().Throw<Newtonsoft.Json.JsonReaderException>();
        }

        [Fact]
        public void GetTestsForFolder_HandlesMissingFolder()
        {
            // Given
            var nonExistentFolder = "NonExistentFolder";

            // When/Then
            Action act = () => TestReader.GetTestsForAFolder(nonExistentFolder).ToList();
            act.Should().Throw<DirectoryNotFoundException>();
        }

        [Fact]
        public void GetTestsForFile_HandlesNoReadPermissions()
        {
            // Given
            var fileName = "nopermission.json";
            var filePath = Path.Combine(_testFolderPath, fileName);
            File.WriteAllText(filePath, @"{'test':{'key':'value'}}");

            try
            {
                // On Unix systems, we need to use different permission setting
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"000 {filePath}",
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                }
                else
                {
                    var fileInfo = new FileInfo(filePath);
                    fileInfo.IsReadOnly = true;
                }

                // When/Then
                Action act = () => TestReader.GetTestsForAFile(TestFolder, fileName).ToList();
                act.Should().Throw<UnauthorizedAccessException>();
            }
            finally
            {
                // Cleanup - restore permissions so the file can be deleted
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"666 {filePath}",
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        }
                    };
                    process.Start();
                    process.WaitForExit();
                }
            }
        }

        [Fact]
        public void GetTestsForFile_HandlesSpecialCharactersInFilename()
        {
            // Given
            var fileName = "test@#$%.json";
            File.WriteAllText(Path.Combine(TestFolder, fileName), @"{'test':{'key':'value'}}");

            // When
            var result = TestReader.GetTestsForAFile(TestFolder, fileName).ToList();

            // Then
            result.Should().HaveCount(1);
        }

        [Fact]
        public void GetTestsForFile_HandlesLargeJsonFile()
        {
            // Given
            var fileName = "large.json";
            var sb = new StringBuilder();
            sb.Append("{");
            for (int i = 0; i < 10000; i++)
            {
                sb.Append($"'test{i}':{{'key':'value'}},");
            }

            sb.Append("'lastTest':{'key':'value'}}");
            File.WriteAllText(Path.Combine(TestFolder, fileName), sb.ToString());

            // When
            var result = TestReader.GetTestsForAFile(TestFolder, fileName).ToList();

            // Then
            result.Should().HaveCount(10001);
        }

        [Fact]
        public void GetTestsForFile_HandlesConcurrentAccess()
        {
            // Given
            var fileName = "concurrent.json";
            File.WriteAllText(Path.Combine(TestFolder, fileName), @"{'test':{'key':'value'}}");

            // When
            var tasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
                TestReader.GetTestsForAFile(TestFolder, fileName).ToList()));

            // Then
            var results = Task.WhenAll(tasks).Result;
            results.Should().AllSatisfy(result => result.Should().HaveCount(1));
        }
    }
}
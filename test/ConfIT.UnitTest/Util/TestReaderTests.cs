namespace ConfIT.UnitTest.Util;

public class TestReaderTests : IDisposable
{
    private readonly string _testFolderPath;

    public TestReaderTests()
    {
        _testFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "TestData");
        if (Directory.Exists(_testFolderPath))
            Directory.Delete(_testFolderPath, true);
        Directory.CreateDirectory(_testFolderPath);
    }

    public void Dispose() => Directory.Delete(_testFolderPath, true);

    [Fact]
    public void GetTestsForFile_WithValidJson_ReturnsTestCases()
    {
        // Given
        var fileName = "valid.json";
        File.WriteAllText(Path.Combine(_testFolderPath, fileName), @"{'test1':{'key':'value'}}");

        // When
        var result = TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

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
        File.WriteAllText(Path.Combine(_testFolderPath, fileName), @"{'test1':{'prop':'val'},'test2':{'prop2':'val2'}}");

        // When
        var result = TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

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
        File.WriteAllText(Path.Combine(_testFolderPath, "ignore.txt"), "not json");

        // When
        var result = TestReader.GetTestsForAFolder(_testFolderPath).ToList();

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
        File.WriteAllText(Path.Combine(_testFolderPath, fileName), @"{'test1':{'nested':{'deep':{'value':123}}}}");

        // When
        var result = TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

        // Then
        result.Should().HaveCount(1);
        result[0][1].Should().BeAssignableTo<JToken>();
    }

    [Fact]
    public void GetTestsForFile_ReturnsEmptyForNoTestCases()
    {
        // Given
        var fileName = "empty-object.json";
        File.WriteAllText(Path.Combine(_testFolderPath, fileName), "{}");

        // When
        var result = TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

        // Then
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetTestsForFile_ThrowsOnEmptyJsonFile()
    {
        // Given
        var fileName = "empty-file.json";
        File.WriteAllText(Path.Combine(_testFolderPath, fileName), "");

        // When
        var act = () => TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

        // Then
        act.Should().Throw<Newtonsoft.Json.JsonReaderException>();
    }

    [Fact]
    public void GetTestsForFile_ThrowsOnInvalidJsonFormat()
    {
        // Given
        var fileName = "invalid.json";
        File.WriteAllText(Path.Combine(_testFolderPath, fileName), "{invalid json}");

        // When
        var act = () => TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

        // Then
        act.Should().Throw<Newtonsoft.Json.JsonReaderException>();
    }

    [Fact]
    public void GetTestsForFolder_ThrowsOnMissingFolder()
    {
        // When
        var act = () => TestReader.GetTestsForAFolder("NonExistentFolder").ToList();

        // Then
        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void GetTestsForFile_HandlesSpecialCharactersInFilename()
    {
        // Given
        var fileName = "test-special.json";
        File.WriteAllText(Path.Combine(_testFolderPath, fileName), @"{'test':{'key':'value'}}");

        // When
        var result = TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

        // Then
        result.Should().HaveCount(1);
    }
}

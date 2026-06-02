using Newtonsoft.Json;

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

    public void Dispose()
    {
        Directory.Delete(_testFolderPath, true);
    }

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
        File.WriteAllText(Path.Combine(_testFolderPath, fileName),
            @"{'test1':{'prop':'val'},'test2':{'prop2':'val2'}}");

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
        act.Should().Throw<JsonReaderException>();
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
        act.Should().Throw<JsonReaderException>();
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

    // ── YAML support ──────────────────────────────────────────────────────────

    [Fact]
    public void GetTestsForFile_WithYamlExtension_ReturnsTestCases()
    {
        // Given
        var fileName = "valid.yaml";
        File.WriteAllText(Path.Combine(_testFolderPath, fileName),
            "test1:\n  key: value");

        // When
        var result = TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

        // Then
        result.Should().HaveCount(1);
        result[0][0].Should().Be("test1");
        result[0][1].Should().BeAssignableTo<JToken>();
    }

    [Fact]
    public void GetTestsForFile_WithYmlExtension_ReturnsTestCases()
    {
        // Given — .yml (not .yaml) is also recognised
        var fileName = "valid.yml";
        File.WriteAllText(Path.Combine(_testFolderPath, fileName),
            "test1:\n  key: value");

        // When
        var result = TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

        // Then
        result.Should().HaveCount(1);
        result[0][0].Should().Be("test1");
    }

    [Fact]
    public void GetTestsForFolder_DiscoversBothJsonAndYamlFiles()
    {
        // Given
        File.WriteAllText(Path.Combine(_testFolderPath, "json-tests.json"),
            @"{'jsonTest':{'key':'value'}}");
        File.WriteAllText(Path.Combine(_testFolderPath, "yaml-tests.yaml"),
            "yamlTest:\n  key: value");

        // When
        var result = TestReader.GetTestsForAFolder(_testFolderPath).ToList();

        // Then
        result.Should().HaveCount(2);
        result.Should().Contain(x => x[0].ToString() == "jsonTest");
        result.Should().Contain(x => x[0].ToString() == "yamlTest");
    }

    [Fact]
    public void GetTestsForFolder_DiscoversBothYamlAndYmlExtensions()
    {
        // Given
        File.WriteAllText(Path.Combine(_testFolderPath, "a.yaml"), "test1:\n  key: value");
        File.WriteAllText(Path.Combine(_testFolderPath, "b.yml"), "test2:\n  key: value");

        // When
        var result = TestReader.GetTestsForAFolder(_testFolderPath).ToList();

        // Then
        result.Should().HaveCount(2);
        result.Should().Contain(x => x[0].ToString() == "test1");
        result.Should().Contain(x => x[0].ToString() == "test2");
    }

    [Fact]
    public void GetTestsForFile_InvalidYaml_ThrowsInvalidDataExceptionWithFilename()
    {
        // Given
        var fileName = "bad.yaml";
        var filePath = Path.GetFullPath(Path.Combine(_testFolderPath, fileName));
        File.WriteAllText(filePath, "root:\n\tchild: value");

        // When
        var act = () => TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

        // Then
        act.Should().Throw<InvalidDataException>()
            .WithMessage($"*{filePath}*");
    }

    [Fact]
    public void GetTestsForFile_YamlTypes_SurviveRoundTripToJToken()
    {
        // Given — ensures the format adapter preserves types the DSL relies on
        var fileName = "typed.yaml";
        File.WriteAllText(Path.Combine(_testFolderPath, fileName), """
                                                                   TypedTest:
                                                                     statusCode: 201
                                                                     flag: true
                                                                     name: alice
                                                                   """);

        // When
        var result = TestReader.GetTestsForAFile(_testFolderPath, fileName).ToList();

        // Then
        var body = (JObject)result[0][1];
        body["statusCode"]!.Type.Should().Be(JTokenType.Integer);
        body["flag"]!.Type.Should().Be(JTokenType.Boolean);
        body["name"]!.Type.Should().Be(JTokenType.String);
    }
}
namespace ConfIT.UnitTest.Reader;

public class TestCaseResolverTests : IDisposable
{
    private readonly string _folder;

    public TestCaseResolverTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, true); } catch { /* best-effort */ }
    }

    private string WriteJson(string content)
    {
        var path = Path.Combine(_folder, Path.GetRandomFileName() + ".json");
        File.WriteAllText(path, content);
        return Path.GetFileName(path);
    }

    private static TestCase RawCase(
        string? requestBodyFromFile = null,
        string? responseBodyFromFile = null,
        JToken? requestOverride = null,
        JToken? responseOverride = null) => new()
    {
        Api = new TestApi
        {
            Request  = new HttpTestRequest  { Method = "GET", Path = "/test", BodyFromFile = requestBodyFromFile,  Override = requestOverride },
            Response = new HttpTestResponse { StatusCode = 200,              BodyFromFile = responseBodyFromFile, Override = responseOverride }
        }
    };

    #region No BodyFromFile

    [Fact]
    public void Resolve_WithNoBodyFromFile_LeavesBodyAsIs()
    {
        // Given
        var body = JToken.Parse(@"{""name"":""Alice""}");
        var raw  = RawCase();
        raw.Api.Request.Body = body;

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Body!["name"]!.Value<string>().Should().Be("Alice");
    }

    [Fact]
    public void Resolve_NullFolders_WithNoBodyFromFile_Succeeds()
    {
        // Given
        var raw = RawCase();

        // When / Then — null folders are fine when BodyFromFile is not set
        var act = () => TestCaseResolver.Resolve(raw, null, null);
        act.Should().NotThrow();
    }

    #endregion

    #region BodyFromFile loading

    [Fact]
    public void Resolve_RequestBodyFromFile_LoadsBodyFromDisk()
    {
        // Given
        var file = WriteJson(@"{""key"":""value""}");
        var raw  = RawCase(requestBodyFromFile: file);

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Body!["key"]!.Value<string>().Should().Be("value");
    }

    [Fact]
    public void Resolve_ResponseBodyFromFile_LoadsBodyFromDisk()
    {
        // Given
        var file = WriteJson(@"{""id"":42}");
        var raw  = RawCase(responseBodyFromFile: file);

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Response.Body!["id"]!.Value<int>().Should().Be(42);
    }

    [Fact]
    public void Resolve_BodyFromFileAndBodySet_FileBodyWins()
    {
        // Given — BodyFromFile supersedes any inline Body
        var file = WriteJson(@"{""source"":""file""}");
        var raw  = RawCase(requestBodyFromFile: file);
        raw.Api.Request.Body = JToken.Parse(@"{""source"":""inline""}");

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Body!["source"]!.Value<string>().Should().Be("file");
    }

    #endregion

    #region Override merging

    [Fact]
    public void Resolve_WithOverride_MergesOnTopOfFileBody()
    {
        // Given
        var file     = WriteJson(@"{""name"":""base"",""version"":1}");
        var override_ = JObject.Parse(@"{""name"":""overridden""}");
        var raw      = RawCase(requestBodyFromFile: file, requestOverride: override_);

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Body!["name"]!.Value<string>().Should().Be("overridden");
        result.Api.Request.Body!["version"]!.Value<int>().Should().Be(1);
    }

    [Fact]
    public void Resolve_WithArrayBodyAndOverride_MergesIntoEachItem()
    {
        // Given
        var file     = WriteJson(@"[{""a"":1},{""a"":2}]");
        var override_ = JObject.Parse(@"{""b"":99}");
        var raw      = RawCase(requestBodyFromFile: file, requestOverride: override_);

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        var arr = (JArray)result.Api.Request.Body!;
        arr[0]["b"]!.Value<int>().Should().Be(99);
        arr[1]["b"]!.Value<int>().Should().Be(99);
    }

    #endregion

    #region Mock interactions

    [Fact]
    public void Resolve_MockInteractionBodyFromFile_HydratesEachInteraction()
    {
        // Given
        var file = WriteJson(@"{""mocked"":true}");
        var raw  = new TestCase
        {
            Api  = new TestApi { Request = new HttpTestRequest { Method = "GET", Path = "/" }, Response = new HttpTestResponse { StatusCode = 200 } },
            Mock = new TestMock
            {
                Interactions =
                [
                    new MockInteraction
                    {
                        Request  = new HttpTestRequest  { Method = "GET", Path = "/dep" },
                        Response = new HttpTestResponse { StatusCode = 200, BodyFromFile = file }
                    }
                ]
            }
        };

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Mock!.Interactions[0].Response.Body!["mocked"]!.Value<bool>().Should().BeTrue();
    }

    #endregion

    #region Error cases

    [Fact]
    public void Resolve_FileNotFound_ThrowsWithUsefulMessage()
    {
        // Given
        var raw = RawCase(requestBodyFromFile: "nonexistent.json");

        // When / Then
        var act = () => TestCaseResolver.Resolve(raw, _folder, _folder);
        act.Should().Throw<Exception>().Which.Message.Should().Contain("nonexistent.json");
    }

    [Fact]
    public void Resolve_BodyFromFileSetButFolderNull_ThrowsWithMessage()
    {
        // Given
        var raw = RawCase(requestBodyFromFile: "something.json");

        // When / Then
        var act = () => TestCaseResolver.Resolve(raw, null, null);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*bodyFromFile*something.json*");
    }

    [Fact]
    public void Resolve_DoesNotMutateOriginal()
    {
        // Given
        var file = WriteJson(@"{""loaded"":true}");
        var raw  = RawCase(requestBodyFromFile: file);

        // When
        TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then — original BodyFromFile still set, Body still null
        raw.Api.Request.BodyFromFile.Should().Be(file);
        raw.Api.Request.Body.Should().BeNull();
    }

    #endregion
}

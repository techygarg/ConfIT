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

    private string WriteGraphqlFile(string content)
    {
        var path = Path.Combine(_folder, Path.GetRandomFileName() + ".graphql");
        File.WriteAllText(path, content);
        return Path.GetFileName(path);
    }

    private static TestCase RawCase(
        string? requestBodyFromFile = null,
        string? responseBodyFromFile = null,
        JToken? requestOverride = null,
        JToken? responseOverride = null,
        GraphqlRequest? graphql = null) => new()
    {
        Api = new TestApi
        {
            Request  = new HttpTestRequest  { Method = "GET", Path = "/test", BodyFromFile = requestBodyFromFile,  Override = requestOverride, Graphql = graphql },
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

    #region Graphql hydration

    [Fact]
    public void Resolve_GraphqlWithInlineQuery_ComposesBodyWithQueryOnly()
    {
        // Given
        var raw = RawCase(graphql: new GraphqlRequest { Query = "query { me }" });

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        var body = (JObject)result.Api.Request.Body!;
        body["query"]!.Value<string>().Should().Be("query { me }");
        ((IDictionary<string, JToken?>)body).ContainsKey("variables").Should().BeFalse();
        ((IDictionary<string, JToken?>)body).ContainsKey("operationName").Should().BeFalse();
    }

    [Fact]
    public void Resolve_GraphqlWithVariables_ComposesBodyWithVariables()
    {
        // Given
        var variables = JObject.Parse(@"{""id"":1}");
        var raw = RawCase(graphql: new GraphqlRequest { Query = "query($id: ID!) { user(id: $id) { name } }", Variables = variables });

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Body!["variables"]!["id"]!.Value<int>().Should().Be(1);
    }

    [Fact]
    public void Resolve_GraphqlWithOperationName_ComposesBodyWithOperationName()
    {
        // Given
        var raw = RawCase(graphql: new GraphqlRequest { Query = "query Me { me }", OperationName = "Me" });

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Body!["operationName"]!.Value<string>().Should().Be("Me");
    }

    [Fact]
    public void Resolve_GraphqlQueryFromFile_ReadsRawFileTextByteForByte()
    {
        // Given
        const string queryText = "query Me {\n  me {\n    id\n    name\n  }\n}\n";
        var file = WriteGraphqlFile(queryText);
        var raw = RawCase(graphql: new GraphqlRequest { QueryFromFile = file });

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Body!["query"]!.Value<string>().Should().Be(queryText);
    }

    [Fact]
    public void Resolve_GraphqlQueryAndQueryFromFileSet_FileWins()
    {
        // Given
        var file = WriteGraphqlFile("query FromFile { x }");
        var raw = RawCase(graphql: new GraphqlRequest { Query = "query Inline { y }", QueryFromFile = file });

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Body!["query"]!.Value<string>().Should().Be("query FromFile { x }");
    }

    [Fact]
    public void Resolve_GraphqlQueryFromFileSetButFolderNull_ThrowsWithMessage()
    {
        // Given
        var raw = RawCase(graphql: new GraphqlRequest { QueryFromFile = "something.graphql" });

        // When / Then
        var act = () => TestCaseResolver.Resolve(raw, null, null);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*queryFromFile*something.graphql*");
    }

    [Fact]
    public void Resolve_GraphqlEmptyBlock_ThrowsInvalidOperationException()
    {
        // Given
        var raw = RawCase(graphql: new GraphqlRequest());

        // When / Then
        var act = () => TestCaseResolver.Resolve(raw, _folder, _folder);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'graphql' block must set either 'query' or 'queryFromFile'*");
    }

    [Fact]
    public void Resolve_GraphqlSetWithoutMethod_DefaultsMethodToPost()
    {
        // Given
        var raw = RawCase(graphql: new GraphqlRequest { Query = "query { me }" });
        raw.Api.Request.Method = null!;

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Method.Should().Be("POST");
    }

    [Fact]
    public void Resolve_GraphqlSetWithExplicitMethod_PreservesMethod()
    {
        // Given
        var raw = RawCase(graphql: new GraphqlRequest { Query = "query { me }" });
        raw.Api.Request.Method = "PUT";

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Method.Should().Be("PUT");
    }

    [Fact]
    public void Resolve_GraphqlSetWithNoHeaders_AddsJsonContentType()
    {
        // Given
        var raw = RawCase(graphql: new GraphqlRequest { Query = "query { me }" });

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Headers!["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public void Resolve_GraphqlSetWithExplicitContentType_PreservesHeader()
    {
        // Given
        var raw = RawCase(graphql: new GraphqlRequest { Query = "query { me }" });
        raw.Api.Request.Headers = new Dictionary<string, string> { ["Content-Type"] = "application/graphql" };

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Headers!["Content-Type"].Should().Be("application/graphql");
    }

    [Fact]
    public void Resolve_GraphqlSetWithDifferentlyCasedContentType_PreservesHeaderWithoutDuplicate()
    {
        // Given — header lookup must be case-insensitive so an author's "content-type" isn't duplicated
        var raw = RawCase(graphql: new GraphqlRequest { Query = "query { me }" });
        raw.Api.Request.Headers = new Dictionary<string, string> { ["content-type"] = "application/graphql" };

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        result.Api.Request.Headers.Should().HaveCount(1);
        result.Api.Request.Headers!["content-type"].Should().Be("application/graphql");
    }

    [Fact]
    public void Resolve_MockInteractionGraphql_HydratesSameAsApiRequest()
    {
        // Given
        var raw = new TestCase
        {
            Api  = new TestApi { Request = new HttpTestRequest { Method = "GET", Path = "/test" }, Response = new HttpTestResponse { StatusCode = 200 } },
            Mock = new TestMock
            {
                Interactions =
                [
                    new MockInteraction
                    {
                        Request  = new HttpTestRequest { Path = "/dep", Graphql = new GraphqlRequest { Query = "query { dep }" } },
                        Response = new HttpTestResponse { StatusCode = 200 }
                    }
                ]
            }
        };

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        var interactionRequest = result.Mock!.Interactions[0].Request;
        interactionRequest.Body!["query"]!.Value<string>().Should().Be("query { dep }");
        interactionRequest.Method.Should().Be("POST");
        interactionRequest.Headers!["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public void Resolve_GraphqlAndBodyFromFileBothSet_GraphqlBodyWins()
    {
        // Given — graphql hydration runs after bodyFromFile hydration, so it overwrites the file-loaded body
        var file = WriteJson(@"{""source"":""file""}");
        var raw  = RawCase(requestBodyFromFile: file, graphql: new GraphqlRequest { Query = "query { me }" });

        // When
        var result = TestCaseResolver.Resolve(raw, _folder, _folder);

        // Then
        var body = (JObject)result.Api.Request.Body!;
        body["query"]!.Value<string>().Should().Be("query { me }");
        ((IDictionary<string, JToken?>)body).ContainsKey("source").Should().BeFalse();
    }

    #endregion
}

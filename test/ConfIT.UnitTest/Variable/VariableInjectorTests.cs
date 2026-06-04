namespace ConfIT.UnitTest.Variable;

public class VariableInjectorTests
{
    private static VariableStore StoreWith(string testName, string varName, JToken value)
    {
        var store = new VariableStore();
        store.Set(testName, varName, value);
        return store;
    }

    private static TestCase BuildTestCase(
        string path = "/api/users",
        string method = "GET",
        JToken? requestBody = null,
        JToken? responseBody = null,
        Dictionary<string, string>? requestHeaders = null,
        Dictionary<string, string>? requestParams = null)
    {
        return new TestCase
        {
            Api = new TestApi
            {
                Request = new HttpTestRequest
                {
                    Method = method,
                    Path = path,
                    Body = requestBody,
                    Headers = requestHeaders,
                    Params = requestParams
                },
                Response = new HttpTestResponse { StatusCode = 200, Body = responseBody }
            }
        };
    }

    #region API injection

    [Fact]
    public void Inject_PathWithVariable_ReplacesWithStringValue()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "userId", new JValue("abc-123"));
        var testCase = BuildTestCase("/api/users/{{userId}}");

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Api.Request.Path.Should().Be("/api/users/abc-123");
    }

    [Fact]
    public void Inject_PathWithFullPrefix_ResolvesCorrectly()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "userId", new JValue("abc-123"));
        var testCase = BuildTestCase("/api/users/{{ShouldCreateUser.userId}}");

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Api.Request.Path.Should().Be("/api/users/abc-123");
    }

    [Fact]
    public void Inject_BodyWithExactVariable_PreservesNumberType()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "score", new JValue(42));
        var testCase = BuildTestCase(requestBody: JToken.Parse("""{"score":"{{score}}"}"""));

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Api.Request.Body!["score"]!.Type.Should().Be(JTokenType.Integer);
        result.Api.Request.Body!["score"]!.Value<int>().Should().Be(42);
    }

    [Fact]
    public void Inject_BodyWithEmbeddedVariable_StringifiesValue()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "userId", new JValue("abc-123"));
        var testCase = BuildTestCase(requestBody: JToken.Parse("""{"ref":"user-{{userId}}-v1"}"""));

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Api.Request.Body!["ref"]!.Value<string>().Should().Be("user-abc-123-v1");
    }

    [Fact]
    public void Inject_Params_ReplacesValues()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "userEmail", new JValue("alice@example.com"));
        var testCase = BuildTestCase(requestParams: new Dictionary<string, string> { { "filter", "{{userEmail}}" } });

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Api.Request.Params!["filter"].Should().Be("alice@example.com");
    }

    [Fact]
    public void Inject_Headers_ReplacesValues()
    {
        // Given
        var store = StoreWith("ShouldGetToken", "authToken", new JValue("token-xyz"));
        var testCase = BuildTestCase(requestHeaders: new Dictionary<string, string>
        {
            { "Authorization", "Bearer {{authToken}}" }
        });

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Api.Request.Headers!["Authorization"].Should().Be("Bearer token-xyz");
    }

    [Fact]
    public void Inject_ResponseBody_ReplacesVariableInExpectedAssertion()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "userId", new JValue("abc-123"));
        var testCase = BuildTestCase(responseBody: JToken.Parse("""{"id":"{{userId}}"}"""));

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Api.Response.Body!["id"]!.Value<string>().Should().Be("abc-123");
    }

    [Fact]
    public void Inject_NestedBodyObject_ReplacesVariables()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "userId", new JValue("abc-123"));
        var testCase = BuildTestCase(requestBody: JToken.Parse("""{"user":{"id":"{{userId}}","name":"Alice"}}"""));

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Api.Request.Body!["user"]!["id"]!.Value<string>().Should().Be("abc-123");
        result.Api.Request.Body!["user"]!["name"]!.Value<string>().Should().Be("Alice");
    }

    #endregion

    #region Mock interaction injection

    [Fact]
    public void Inject_MockInteractionPath_ReplacesVariable()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "userId", new JValue("abc-123"));
        var testCase = BuildTestCase();
        testCase.Mock = new TestMock
        {
            Interactions =
            [
                new MockInteraction
                {
                    Request = new HttpTestRequest { Method = "GET", Path = "/api/users/{{userId}}" },
                    Response = new HttpTestResponse { StatusCode = 200 }
                }
            ]
        };

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Mock!.Interactions[0].Request.Path.Should().Be("/api/users/abc-123");
    }

    [Fact]
    public void Inject_MockInteractionResponseBody_ReplacesVariable()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "userId", new JValue("abc-123"));
        var testCase = BuildTestCase();
        testCase.Mock = new TestMock
        {
            Interactions =
            [
                new MockInteraction
                {
                    Request = new HttpTestRequest { Method = "GET", Path = "/api/users" },
                    Response = new HttpTestResponse
                    {
                        StatusCode = 200,
                        Body = JToken.Parse("""{"id":"{{userId}}"}""")
                    }
                }
            ]
        };

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Mock!.Interactions[0].Response.Body!["id"]!.Value<string>().Should().Be("abc-123");
    }

    #endregion

    #region Environment variables

    [Fact]
    public void Inject_EnvVariable_ResolvesFromEnvironment()
    {
        // Given
        Environment.SetEnvironmentVariable("TEST_API_KEY", "secret-key-123");
        var store = new VariableStore();
        var testCase = BuildTestCase(requestHeaders: new Dictionary<string, string>
        {
            { "X-Api-Key", "${TEST_API_KEY}" }
        });

        try
        {
            // When
            var result = VariableInjector.Inject(testCase, store);

            // Then
            result.Api.Request.Headers!["X-Api-Key"].Should().Be("secret-key-123");
        }
        finally
        {
            Environment.SetEnvironmentVariable("TEST_API_KEY", null);
        }
    }

    #endregion

    #region Isolation and error cases

    [Fact]
    public void Inject_UndefinedVariable_ThrowsUndefinedException()
    {
        // Given
        var store = new VariableStore();
        var testCase = BuildTestCase("/api/users/{{userId}}");

        // When
        var act = () => VariableInjector.Inject(testCase, store);

        // Then
        act.Should().Throw<UndefinedVariableException>().WithMessage("*userId*");
    }

    [Fact]
    public void Inject_OriginalTestCase_IsNotMutated()
    {
        // Given
        var store = StoreWith("ShouldCreateUser", "userId", new JValue("abc-123"));
        var testCase = BuildTestCase("/api/users/{{userId}}");
        var originalPath = testCase.Api.Request.Path;

        // When
        VariableInjector.Inject(testCase, store);

        // Then
        testCase.Api.Request.Path.Should().Be(originalPath);
    }

    [Fact]
    public void Inject_NoVariables_ReturnsEquivalentCopy()
    {
        // Given
        var store = new VariableStore();
        var testCase = BuildTestCase("/api/users/123");

        // When
        var result = VariableInjector.Inject(testCase, store);

        // Then
        result.Api.Request.Path.Should().Be("/api/users/123");
    }

    #endregion
}
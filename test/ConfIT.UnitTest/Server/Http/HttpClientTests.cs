namespace ConfIT.UnitTest.Server.Http;

public class HttpClientTests
{
    private const string BaseUrl          = "http://test.com";
    private const string DefaultPath      = "/api/test";
    private const string DefaultAuthToken = "Bearer test-token";

    private static readonly JToken DefaultRequestBody = JToken.Parse("{ \"key\": \"value\" }");

    private readonly Mock<IAuthTokenProvider>  _mockAuthTokenProvider;
    private readonly Mock<HttpMessageHandler>  _mockHttpMessageHandler;
    private readonly TestHttpClient            _testHttpClient;

    public HttpClientTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockAuthTokenProvider  = new Mock<IAuthTokenProvider>();
        var client              = new HttpClient(_mockHttpMessageHandler.Object) { BaseAddress = new Uri(BaseUrl) };
        _testHttpClient         = new TestHttpClient(client, _mockAuthTokenProvider.Object);
    }

    private static TestApi CreateTestApi(
        string method,
        string path                        = DefaultPath,
        JToken? body                       = null,
        Dictionary<string, string>? headers = null)
    {
        return new TestApi
        {
            Request = new HttpTestRequest
            {
                Method  = method,
                Path    = path,
                Body    = body,
                Headers = headers
            }
        };
    }

    private void SetupMockHandler(HttpStatusCode statusCode)
    {
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
    }

    private void VerifyHttpCall(HttpMethod method, string path, Times times)
    {
        _mockHttpMessageHandler
            .Protected()
            .Verify("SendAsync", times,
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == method &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString() == $"{BaseUrl}{path}"),
                ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyHttpCallWithHeaders(Dictionary<string, string> headers)
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    headers.All(header =>
                        req.Headers.Contains(header.Key) &&
                        req.Headers.GetValues(header.Key).First() == header.Value)),
                ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyAuthorizationHeader(string expectedToken)
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Headers.Authorization != null &&
                    req.Headers.Authorization.ToString() == expectedToken),
                ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyHttpCallToBaseUrl()
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString() == $"{BaseUrl}/"),
                ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyEmptyRequestContent()
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Content != null &&
                    req.Content.ReadAsStringAsync().Result == string.Empty),
                ItExpr.IsAny<CancellationToken>());
    }

    public class HttpMethodTests : HttpClientTests
    {
        [Fact]
        public async Task Execute_GetMethod_SendsGetRequest()
        {
            // Given
            var testApi = CreateTestApi("GET");
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyHttpCall(HttpMethod.Get, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task Execute_PostWithBody_SendsPostRequest()
        {
            // Given
            var testApi = CreateTestApi("POST", body: DefaultRequestBody);
            SetupMockHandler(HttpStatusCode.Created);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            VerifyHttpCall(HttpMethod.Post, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task Execute_PutWithBody_SendsPutRequest()
        {
            // Given
            var testApi = CreateTestApi("PUT", body: DefaultRequestBody);
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyHttpCall(HttpMethod.Put, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task Execute_PatchWithBody_SendsPatchRequest()
        {
            // Given
            var testApi = CreateTestApi("PATCH", body: DefaultRequestBody);
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyHttpCall(HttpMethod.Patch, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task Execute_DeleteMethod_SendsDeleteRequest()
        {
            // Given
            var testApi = CreateTestApi("DELETE");
            SetupMockHandler(HttpStatusCode.NoContent);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            VerifyHttpCall(HttpMethod.Delete, DefaultPath, Times.Once());
        }
    }

    public class HeaderTests : HttpClientTests
    {
        [Fact]
        public async Task Execute_WithCustomHeaders_IncludesHeadersInRequest()
        {
            // Given
            var headers = new Dictionary<string, string> { { "Custom-Header", "TestValue" } };
            var testApi = CreateTestApi("GET", headers: headers);
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyHttpCallWithHeaders(headers);
        }

        [Fact]
        public async Task Execute_WithAuthToken_IncludesAuthorizationHeader()
        {
            // Given
            var testApi = CreateTestApi("GET");
            _mockAuthTokenProvider.Setup(x => x.HeaderKey()).Returns("Authorization");
            _mockAuthTokenProvider.Setup(x => x.Token()).Returns(DefaultAuthToken);
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyAuthorizationHeader(DefaultAuthToken);
        }

        [Fact]
        public async Task Execute_WithApiKeyAuth_UsesCustomHeader()
        {
            // Given
            var testApi = CreateTestApi("GET");
            _mockAuthTokenProvider.Setup(x => x.HeaderKey()).Returns("X-API-Key");
            _mockAuthTokenProvider.Setup(x => x.Token()).Returns("my-api-key");
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyHttpCallWithHeaders(new Dictionary<string, string> { { "X-API-Key", "my-api-key" } });
        }

        [Fact]
        public async Task Execute_WithMultipleHeaders_IncludesAllInRequest()
        {
            // Given
            var headers = new Dictionary<string, string>
            {
                { "Header1", "Value1" },
                { "Header2", "Value2" },
                { "Header3", "Value3" }
            };
            var testApi = CreateTestApi("GET", headers: headers);
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyHttpCallWithHeaders(headers);
        }

        [Fact]
        public async Task Execute_NullHeaders_DoesNotThrow()
        {
            // Given
            var testApi = CreateTestApi("GET", headers: null);
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyHttpCall(HttpMethod.Get, DefaultPath, Times.Once());
        }
    }

    public class EdgeCaseTests : HttpClientTests
    {
        [Fact]
        public async Task Execute_EmptyPath_SendsToBaseUrl()
        {
            // Given
            var testApi = CreateTestApi("GET", "");
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyHttpCallToBaseUrl();
        }

        [Fact]
        public async Task Execute_NullBody_SendsEmptyContent()
        {
            // Given
            var testApi = CreateTestApi("POST", body: null);
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyEmptyRequestContent();
        }

        [Fact]
        public async Task Execute_LowercaseMethod_SendsNormalizedRequest()
        {
            // Given
            var testApi = CreateTestApi("get");
            SetupMockHandler(HttpStatusCode.OK);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            VerifyHttpCall(HttpMethod.Get, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task Execute_ServerReturnsError_ReturnsErrorStatusCode()
        {
            // Given
            var testApi = CreateTestApi("GET");
            SetupMockHandler(HttpStatusCode.InternalServerError);

            // When
            var response = await _testHttpClient.Execute(testApi);

            // Then
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
    }

    public class ValidationTests : HttpClientTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_InvalidServerUrl_ThrowsArgumentException(string serverUrl)
        {
            // When
            var action = () => TestHttpClient.Create(serverUrl, _mockAuthTokenProvider.Object);

            // Then
            action.Should().Throw<ArgumentException>()
                .WithMessage("Server URL cannot be null or empty*")
                .WithParameterName(nameof(serverUrl));
        }

        [Fact]
        public void Create_ValidServerUrl_ReturnsInstance()
        {
            // When
            var client = TestHttpClient.Create(BaseUrl, _mockAuthTokenProvider.Object);

            // Then
            client.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_NullHttpClient_ThrowsArgumentNullException()
        {
            // When
            var action = () => new TestHttpClient(null);

            // Then
            action.Should().Throw<ArgumentNullException>().WithParameterName("client");
        }

        [Fact]
        public async Task Execute_NullTestApi_ThrowsArgumentNullException()
        {
            // When
            var action = () => _testHttpClient.Execute(null);

            // Then
            await action.Should().ThrowAsync<ArgumentNullException>().WithParameterName("testApi");
        }

        [Fact]
        public async Task Execute_UnsupportedMethod_ThrowsNotSupportedException()
        {
            // Given
            var testApi = CreateTestApi("HEAD");

            // When
            var action = () => _testHttpClient.Execute(testApi);

            // Then
            await action.Should().ThrowAsync<NotSupportedException>()
                .WithMessage("HTTP method 'HEAD' is not supported.");
        }
    }

    public class DisposalTests : HttpClientTests
    {
        [Fact]
        public void Dispose_Called_DisposesUnderlyingHttpClient()
        {
            // Given
            var handler       = new DisposeTrackingHandler();
            var client        = new HttpClient(handler);
            var testHttpClient = new TestHttpClient(client);

            // When
            testHttpClient.Dispose();

            // Then
            handler.WasDisposed.Should().BeTrue();
        }
    }

    private class DisposeTrackingHandler : HttpMessageHandler
    {
        public bool WasDisposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}

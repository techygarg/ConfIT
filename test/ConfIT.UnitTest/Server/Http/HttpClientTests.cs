using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConfIT.Contract;
using ConfIT.Server.Dto;
using ConfIT.Server.Http;
using Moq;
using Moq.Protected;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ConfIT.UnitTest.Server.Http;

public class HttpClientTests
{
    private const string BaseUrl = "http://test.com";
    private const string DefaultPath = "/api/test";
    private const string DefaultAuthToken = "Bearer test-token";
    private static readonly JToken DefaultRequestBody = JToken.Parse("{ \"key\": \"value\" }");

    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly Mock<IAuthTokenProvider> _mockAuthTokenProvider;
    private readonly TestHttpClient _testHttpClient;

    public HttpClientTests()
    {
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _mockAuthTokenProvider = new Mock<IAuthTokenProvider>();
        var client = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(BaseUrl)
        };
        _testHttpClient = new TestHttpClient(client, _mockAuthTokenProvider.Object);
    }

    public class HttpMethodTests : HttpClientTests
    {
        [Fact]
        public async Task Get_ShouldSendGetRequest()
        {
            // Arrange
            var testApi = CreateTestApi("GET");
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyHttpCall(HttpMethod.Get, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task Post_WithBody_ShouldSendPostRequest()
        {
            // Arrange
            var testApi = CreateTestApi("POST", body: DefaultRequestBody);
            SetupMockHandler(HttpStatusCode.Created);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            VerifyHttpCall(HttpMethod.Post, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task Put_WithBody_ShouldSendPutRequest()
        {
            // Arrange
            var testApi = CreateTestApi("PUT", body: DefaultRequestBody);
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyHttpCall(HttpMethod.Put, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task Patch_WithBody_ShouldSendPatchRequest()
        {
            // Arrange
            var testApi = CreateTestApi("PATCH", body: DefaultRequestBody);
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyHttpCall(HttpMethod.Patch, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task Delete_ShouldSendDeleteRequest()
        {
            // Arrange
            var testApi = CreateTestApi("DELETE");
            SetupMockHandler(HttpStatusCode.NoContent);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            VerifyHttpCall(HttpMethod.Delete, DefaultPath, Times.Once());
        }
    }

    public class HeaderTests : HttpClientTests
    {
        [Fact]
        public async Task WithCustomHeaders_ShouldIncludeHeaders()
        {
            // Arrange
            var headers = new Dictionary<string, string> { { "Custom-Header", "TestValue" } };
            var testApi = CreateTestApi("GET", headers: headers);
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyHttpCallWithHeaders(headers);
        }

        [Fact]
        public async Task WithAuthToken_ShouldIncludeAuthorizationHeader()
        {
            // Arrange
            var testApi = CreateTestApi("GET");
            _mockAuthTokenProvider.Setup(x => x.Token()).Returns(DefaultAuthToken);
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyAuthorizationHeader(DefaultAuthToken);
        }

        [Fact]
        public async Task WithMultipleHeaders_ShouldIncludeAllHeaders()
        {
            // Arrange
            var headers = new Dictionary<string, string>
            {
                { "Header1", "Value1" },
                { "Header2", "Value2" },
                { "Header3", "Value3" }
            };
            var testApi = CreateTestApi("GET", headers: headers);
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyHttpCallWithHeaders(headers);
        }

        [Fact]
        public async Task WithNullHeaders_ShouldNotThrowException()
        {
            // Arrange
            var testApi = CreateTestApi("GET", headers: null);
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyHttpCall(HttpMethod.Get, DefaultPath, Times.Once());
        }
    }

    public class EdgeCaseTests : HttpClientTests
    {
        [Fact]
        public async Task WithEmptyPath_ShouldSendRequestToBaseUrl()
        {
            // Arrange
            var testApi = CreateTestApi("GET", path: "");
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyHttpCallToBaseUrl();
        }

        [Fact]
        public async Task WithNullBody_ShouldSendEmptyContent()
        {
            // Arrange
            var testApi = CreateTestApi("POST", body: null);
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyEmptyRequestContent();
        }

        [Fact]
        public async Task WithLowercaseMethod_ShouldHandleCorrectly()
        {
            // Arrange
            var testApi = CreateTestApi("get");
            SetupMockHandler(HttpStatusCode.OK);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            VerifyHttpCall(HttpMethod.Get, DefaultPath, Times.Once());
        }

        [Fact]
        public async Task WithErrorResponse_ShouldReturnErrorStatusCode()
        {
            // Arrange
            var testApi = CreateTestApi("GET");
            SetupMockHandler(HttpStatusCode.InternalServerError);

            // Act
            var response = await _testHttpClient.Execute(testApi);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
    }

    public class ValidationTests : HttpClientTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void Create_WithInvalidServerUrl_ShouldThrowArgumentException(string serverUrl)
        {
            Assert.Throws<ArgumentException>(() => 
                TestHttpClient.Create(serverUrl, _mockAuthTokenProvider.Object));
        }

        [Fact]
        public void Create_WithValidServerUrl_ShouldReturnInstance()
        {
            var client = TestHttpClient.Create(BaseUrl, _mockAuthTokenProvider.Object);
            Assert.NotNull(client);
        }

        [Fact]
        public void Constructor_WithNullHttpClient_ShouldThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TestHttpClient(null));
        }

        [Fact]
        public async Task Execute_WithNullTestApi_ShouldThrowArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _testHttpClient.Execute(null));
        }

        [Fact]
        public async Task Execute_WithUnsupportedMethod_ShouldThrowException()
        {
            var testApi = CreateTestApi("HEAD");
            await Assert.ThrowsAsync<Exception>(() => _testHttpClient.Execute(testApi));
        }
    }

    public class DisposalTests : HttpClientTests
    {
        [Fact]
        public void Dispose_ShouldDisposeHttpClient()
        {
            var handler = new DisposeTrackingHandler();
            var client = new HttpClient(handler);
            var testHttpClient = new TestHttpClient(client);

            testHttpClient.Dispose();

            Assert.True(handler.WasDisposed);
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
            if (disposing)
            {
                WasDisposed = true;
            }
            base.Dispose(disposing);
        }
    }

    private static TestApi CreateTestApi(
        string method, 
        string path = DefaultPath, 
        JToken body = null, 
        Dictionary<string, string> headers = null)
    {
        return new TestApi
        {
            Request = new HttpTestRequest
            {
                Method = method,
                Path = path,
                Body = body,
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
            .Verify("SendAsync",
                times,
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == method &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString() == $"{BaseUrl}{path}"),
                ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyHttpCallWithHeaders(Dictionary<string, string> headers)
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(req =>
                headers.All(header =>
                    req.Headers.Contains(header.Key) &&
                    req.Headers.GetValues(header.Key).First() == header.Value)),
                ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyAuthorizationHeader(string expectedToken)
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.Authorization != null &&
                req.Headers.Authorization.ToString() == expectedToken),
                ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyHttpCallToBaseUrl()
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString() == $"{BaseUrl}/"),
                ItExpr.IsAny<CancellationToken>());
    }

    private void VerifyEmptyRequestContent()
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(req =>
                req.Content != null && 
                req.Content.ReadAsStringAsync().Result == string.Empty),
                ItExpr.IsAny<CancellationToken>());
    }
}
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConfIT.Contract;
using ConfIT.Server.Dto;
using ConfIT.Server.Http;
using FluentAssertions;
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyHttpCallAsync(HttpMethod.Get, DefaultPath, Times.Once());
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
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            await VerifyHttpCallAsync(HttpMethod.Post, DefaultPath, Times.Once());
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyHttpCallAsync(HttpMethod.Put, DefaultPath, Times.Once());
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyHttpCallAsync(HttpMethod.Patch, DefaultPath, Times.Once());
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
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            await VerifyHttpCallAsync(HttpMethod.Delete, DefaultPath, Times.Once());
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyHttpCallWithHeadersAsync(headers);
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyAuthorizationHeaderAsync(DefaultAuthToken);
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyHttpCallWithHeadersAsync(headers);
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
            response.Should().NotBeNull();
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyHttpCallAsync(HttpMethod.Get, DefaultPath, Times.Once());
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyHttpCallToBaseUrlAsync();
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyEmptyRequestContentAsync();
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
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            await VerifyHttpCallAsync(HttpMethod.Get, DefaultPath, Times.Once());
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
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
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
            // Act
            var action = () => TestHttpClient.Create(serverUrl, _mockAuthTokenProvider.Object);

            // Assert
            action.Should().Throw<ArgumentException>()
                .WithMessage("Server URL cannot be null or empty*")
                .WithParameterName(nameof(serverUrl));
        }

        [Fact]
        public void Create_WithValidServerUrl_ShouldReturnInstance()
        {
            // Act
            var client = TestHttpClient.Create(BaseUrl, _mockAuthTokenProvider.Object);

            // Assert
            client.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullHttpClient_ShouldThrowArgumentNullException()
        {
            // Act
            var action = () => new TestHttpClient(null);

            // Assert
            action.Should().Throw<ArgumentNullException>()
                .WithParameterName("client");
        }

        [Fact]
        public async Task Execute_WithNullTestApi_ShouldThrowArgumentNullException()
        {
            // Act
            var action = () => _testHttpClient.Execute(null);

            // Assert
            await action.Should().ThrowAsync<ArgumentNullException>()
                .WithParameterName("testApi");
        }

        [Fact]
        public async Task Execute_WithUnsupportedMethod_ShouldThrowException()
        {
            // Arrange
            var testApi = CreateTestApi("HEAD");

            // Act
            var action = () => _testHttpClient.Execute(testApi);

            // Assert
            await action.Should().ThrowAsync<Exception>()
                .WithMessage("HEAD not supported. Please add.");
        }
    }

    public class DisposalTests : HttpClientTests
    {
        [Fact]
        public void Dispose_ShouldDisposeHttpClient()
        {
            // Arrange
            var handler = new DisposeTrackingHandler();
            var client = new HttpClient(handler);
            var testHttpClient = new TestHttpClient(client);

            // Act
            testHttpClient.Dispose();

            // Assert
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
        JToken? body = null, 
        Dictionary<string, string>? headers = null)
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

    private Task VerifyHttpCallAsync(HttpMethod method, string path, Times times)
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

        return Task.CompletedTask;
    }

    private Task VerifyHttpCallWithHeadersAsync(Dictionary<string, string> headers)
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(req =>
                headers.All(header =>
                    req.Headers.Contains(header.Key) &&
                    req.Headers.GetValues(header.Key).First() == header.Value)),
                ItExpr.IsAny<CancellationToken>());

        return Task.CompletedTask;
    }

    private Task VerifyAuthorizationHeaderAsync(string expectedToken)
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.Authorization != null &&
                req.Headers.Authorization.ToString() == expectedToken),
                ItExpr.IsAny<CancellationToken>());

        return Task.CompletedTask;
    }

    private Task VerifyHttpCallToBaseUrlAsync()
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri != null &&
                    req.RequestUri.ToString() == $"{BaseUrl}/"),
                ItExpr.IsAny<CancellationToken>());

        return Task.CompletedTask;
    }

    private Task VerifyEmptyRequestContentAsync()
    {
        _mockHttpMessageHandler.Protected()
            .Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(req =>
                req.Content != null && 
                req.Content.ReadAsStringAsync().Result == string.Empty),
                ItExpr.IsAny<CancellationToken>());

        return Task.CompletedTask;
    }
}
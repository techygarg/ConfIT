using ConfIT.Runner.Mock;
using ConfIT.UnitTest.Server.Http;

namespace ConfIT.UnitTest.Server.Mock;

public class HttpMockServerTests
{
    private const string GraphqlPath = "/graphql";

    [Fact]
    public async Task Initialize_TwoGraphqlInteractionsSamePath_EachReturnsOwnResponse()
    {
        // Given
        var url = $"http://localhost:{TestPort.GetFree()}";

        var requestBodyA  = JToken.Parse("{'query': 'query A { a }'}");
        var requestBodyB  = JToken.Parse("{'query': 'query B { b }'}");
        var responseBodyA = JToken.Parse("{'data': {'a': 1}}");
        var responseBodyB = JToken.Parse("{'data': {'b': 2}}");

        var mock = new TestMock
        {
            Interactions =
            [
                new MockInteraction
                {
                    Request  = new HttpTestRequest  { Method = "POST", Path = GraphqlPath, Body = requestBodyA },
                    Response = new HttpTestResponse { StatusCode = 200, Body = responseBodyA }
                },
                new MockInteraction
                {
                    Request  = new HttpTestRequest  { Method = "POST", Path = GraphqlPath, Body = requestBodyB },
                    Response = new HttpTestResponse { StatusCode = 200, Body = responseBodyB }
                }
            ]
        };

        using var httpMockServer = new HttpMockServer(url, false);
        httpMockServer.Initialize(mock);

        using var httpClient = new HttpClient { BaseAddress = new Uri(url) };

        // When
        var (statusCodeA, actualBodyA) = await PostGraphqlRequest(httpClient, requestBodyA);
        var (statusCodeB, actualBodyB) = await PostGraphqlRequest(httpClient, requestBodyB);

        // Then
        statusCodeA.Should().Be(HttpStatusCode.OK);
        JToken.DeepEquals(actualBodyA, responseBodyA).Should().BeTrue();

        statusCodeB.Should().Be(HttpStatusCode.OK);
        JToken.DeepEquals(actualBodyB, responseBodyB).Should().BeTrue();
    }

    private static async Task<(HttpStatusCode StatusCode, JToken Body)> PostGraphqlRequest(HttpClient client, JToken requestBody)
    {
        using var content = new StringContent(requestBody.ToString(), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(GraphqlPath, content);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JToken.Parse(body));
    }
}

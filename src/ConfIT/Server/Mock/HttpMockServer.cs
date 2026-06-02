using ConfIT.Server.Dto;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace ConfIT.Server.Mock;

public class HttpMockServer : IDisposable
{
    private static readonly HashSet<string> SupportedMethods = ["GET", "PUT", "POST", "PATCH", "DELETE"];

    private readonly WireMockServer _server;

    public HttpMockServer(string url, bool enableLogs)
    {
        var settings = new WireMockServerSettings { Urls = [url] };
        if (enableLogs)
            settings.Logger = new WireMockConsoleLogger();

        _server = WireMockServer.Start(settings);
    }

    public void Dispose()
    {
        _server?.Dispose();
    }

    public void Initialize(TestMock mock)
    {
        _server.Reset();

        if (mock?.Interactions is not { Count: > 0 }) return;

        foreach (var interaction in mock.Interactions)
        {
            if (interaction?.Request is null || interaction.Response is null) continue;

            if (!SupportedMethods.Contains(interaction.Request.Method.ToUpper()))
                throw new NotSupportedException(
                    $"HTTP method '{interaction.Request.Method}' is not supported.");

            SetUpMock(interaction);
        }
    }

    private void SetUpMock(MockInteraction interaction)
    {
        _server
            .Given(
                Request.Create()
                    .UsingMethod(interaction.Request.Method)
                    .WithPath(interaction.Request.Path)
                    .WithQueryParams(interaction.Request.Params)
                    .WithHeaders(interaction.Request.Headers)
                    .WithBodyIfProvided(interaction.Request.Body)
            )
            .RespondWith(
                Response.Create()
                    .WithStatusCode(interaction.Response.StatusCode)
                    .WithHeadersIfProvided(interaction.Response.Headers)
                    .WithHeader("Content-Type", "application/json")
                    .WithBodyIfProvided(interaction.Response.Body)
            );
    }
}
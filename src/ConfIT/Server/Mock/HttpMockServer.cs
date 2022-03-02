using System;
using ConfIT.Server.Dto;
using WireMock.Logging;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace ConfIT.Server.Mock
{
    public class HttpMockServer : IDisposable
    {
        private readonly WireMockServer _server;

        public HttpMockServer(string url, bool enableLogs)
        {
            var settings = new WireMockServerSettings { Urls = new[] { url } };
            if (enableLogs)
                settings.Logger = new WireMockConsoleLogger();

            _server = WireMockServer.Start(settings);
        }

        public void Initialize(TestMock mock)
        {
            if (mock == null || mock.Interactions?.Count == 0)
                return;

            foreach (var interaction in mock.Interactions)
            {
                switch (interaction.Request.Method.ToUpper())
                {
                    case "GET":
                    case "PUT":
                    case "POST":
                    case "PATCH":
                    case "DELETE":
                        SetUpMock(interaction);
                        break;
                    default:
                        throw new Exception($"{interaction.Request.Method} not supported yet. Pls add required methods support here.");
                }
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

        public void Dispose() =>
            _server?.Dispose();
    }
}
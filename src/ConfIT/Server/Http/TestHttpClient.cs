using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ConfIT.Contract;
using ConfIT.Server.Dto;
using Newtonsoft.Json.Linq;

namespace ConfIT.Server.Http
{
    public class TestHttpClient
    {
        private readonly HttpClient _client;
        private readonly IAuthTokenProvider _tokenProvider;

        public TestHttpClient(HttpClient client, IAuthTokenProvider tokenProvider = default)
        {
            _client = client;
            _tokenProvider = tokenProvider;
        }

        public async Task<HttpResponseMessage> Execute(TestApi testApi)
        {
            AddRequestHeaders(testApi.Request.Headers);
            return testApi.Request.Method.ToUpper() switch
            {
                "GET" => await _client.GetAsync(testApi.Request.Path),
                "PUT" => await _client.PutAsync(testApi.Request.Path, RequestBody(testApi.Request.Body)),
                "PATCH" => await _client.PatchAsync(testApi.Request.Path, RequestBody(testApi.Request.Body)),
                "POST" => await _client.PostAsync(testApi.Request.Path, RequestBody(testApi.Request.Body)),
                "DELETE" => await _client.DeleteAsync(testApi.Request.Path),
                _ => throw new Exception($"{testApi.Request.Method} not supported. Please add.")
            };
        }

        private static StringContent RequestBody(JToken body) =>
            new(body.ToString(), Encoding.UTF8, "application/json");

        private void AddRequestHeaders(Dictionary<string, string> headers)
        {
            _client.DefaultRequestHeaders.Clear();
            if (headers is { Count: > 0 })
                foreach (var (name, value) in headers)
                    _client.DefaultRequestHeaders.Add(name, value);

            if (_tokenProvider != null)
                _client.DefaultRequestHeaders.Add("Authorization", _tokenProvider.Token());
        }

        public void Dispose() =>
            _client.Dispose();

        public static TestHttpClient Create(string serverUrl, IAuthTokenProvider authTokenProvider)
        {
            return new TestHttpClient(
                new HttpClient { BaseAddress = new Uri(serverUrl) },
                authTokenProvider);
        }
    }
}
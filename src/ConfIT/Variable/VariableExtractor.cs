using System.Net.Http;

namespace ConfIT.Variable;

public static class VariableExtractor
{
    public static void Extract(
        string testName,
        HttpResponseMessage response,
        JToken actualBody,
        Dictionary<string, string>? extractSpec,
        VariableStore store)
    {
        if (extractSpec is not { Count: > 0 }) return;

        var unified = BuildUnifiedResponse(response, actualBody);

        foreach (var (varName, path) in extractSpec)
        {
            var value = unified.SelectToken(path)
                ?? throw new InvalidOperationException(
                    $"Extract path '{path}' for variable '{varName}' matched nothing in the response. " +
                    $"Check the JSONPath expression against the actual response structure.");

            store.Set(testName, varName, value.DeepClone());
        }
    }

    private static JObject BuildUnifiedResponse(HttpResponseMessage response, JToken actualBody)
    {
        var headers = new JObject();

        foreach (var header in response.Headers)
            headers[header.Key.ToLowerInvariant()] = header.Value.FirstOrDefault();

        foreach (var header in response.Content.Headers)
            headers[header.Key.ToLowerInvariant()] = header.Value.FirstOrDefault();

        return new JObject
        {
            ["body"]       = actualBody,
            ["headers"]    = headers,
            ["statusCode"] = (int)response.StatusCode
        };
    }
}

using System.IO;
using ConfIT.Model;
using Newtonsoft.Json;
using static System.IO.Path;

namespace ConfIT.Reader;

public static class TestCaseResolver
{
    public static TestCase Resolve(TestCase raw, string? requestFolder, string? responseFolder)
    {
        var json  = JsonConvert.SerializeObject(raw);
        var clone = JsonConvert.DeserializeObject<TestCase>(json)!;

        HydratePayload(clone.Api.Request,  requestFolder);
        HydratePayload(clone.Api.Response, responseFolder);
        HydrateGraphql(clone.Api.Request, requestFolder);

        clone.Mock?.Interactions?.ForEach(interaction =>
        {
            HydratePayload(interaction.Request,  requestFolder);
            HydratePayload(interaction.Response, responseFolder);
            HydrateGraphql(interaction.Request, requestFolder);
        });

        return clone;
    }

    private static void HydratePayload(HttpPayload payload, string? folder)
    {
        if (string.IsNullOrWhiteSpace(payload?.BodyFromFile)) return;

        if (string.IsNullOrWhiteSpace(folder))
            throw new InvalidOperationException(
                $"Test case references 'bodyFromFile: {payload.BodyFromFile}' but no folder was provided.");

        var filePath = GetFullPath($"{folder}/{payload.BodyFromFile}");
        var content  = JToken.Parse(File.ReadAllText(filePath));
        ApplyOverride(content, payload.Override);
        payload.Body = content;
    }

    private static void HydrateGraphql(HttpTestRequest request, string? requestFolder)
    {
        var graphql = request.Graphql;
        if (graphql is null) return;

        if (string.IsNullOrWhiteSpace(graphql.Query) && string.IsNullOrWhiteSpace(graphql.QueryFromFile))
            throw new InvalidOperationException(
                "Test case's 'graphql' block must set either 'query' or 'queryFromFile'.");

        var query = ResolveQueryText(graphql, requestFolder);

        var body = new JObject { ["query"] = query };
        // Variables round-trips through Resolve's JSON clone as a JValue(Null), not a C# null, when absent
        if (graphql.Variables is { Type: not JTokenType.Null }) body["variables"] = graphql.Variables;
        if (!string.IsNullOrWhiteSpace(graphql.OperationName)) body["operationName"] = graphql.OperationName;
        request.Body = body;

        if (string.IsNullOrWhiteSpace(request.Method)) request.Method = "POST";

        request.Headers ??= new Dictionary<string, string>();
        var hasContentType = request.Headers.Keys.Any(key => string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase));
        if (!hasContentType) request.Headers["Content-Type"] = "application/json";
    }

    private static string ResolveQueryText(GraphqlRequest graphql, string? requestFolder)
    {
        // queryFromFile wins silently over inline query, mirroring the bodyFromFile/body precedence above
        if (string.IsNullOrWhiteSpace(graphql.QueryFromFile)) return graphql.Query!;

        if (string.IsNullOrWhiteSpace(requestFolder))
            throw new InvalidOperationException(
                $"Test case references 'graphql.queryFromFile: {graphql.QueryFromFile}' but no folder was provided.");

        var filePath = GetFullPath($"{requestFolder}/{graphql.QueryFromFile}");
        return File.ReadAllText(filePath);
    }

    private static void ApplyOverride(JToken payload, JToken? overrideToken)
    {
        if (overrideToken is null) return;

        if (payload is JArray array)
            foreach (var item in array.OfType<JObject>())
                item.Merge(overrideToken);
        else if (payload is JObject obj)
            obj.Merge(overrideToken);
    }
}

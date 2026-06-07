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

        clone.Mock?.Interactions?.ForEach(interaction =>
        {
            HydratePayload(interaction.Request,  requestFolder);
            HydratePayload(interaction.Response, responseFolder);
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

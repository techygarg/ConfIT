using System.Text.RegularExpressions;
using ConfIT.Server.Dto;
using ConfIT.Variable.Exception;
using Newtonsoft.Json;

namespace ConfIT.Variable;

public static class VariableInjector
{
    private static readonly Regex CombinedPattern =
        new(@"\{\{([^}]+)\}\}|\$\{([^}]+)\}", RegexOptions.Compiled);

    private static readonly Regex ExactRuntimePattern =
        new(@"^\{\{([^}]+)\}\}$", RegexOptions.Compiled);

    public static TestCase Inject(TestCase testCase, VariableStore store)
    {
        var json = JsonConvert.SerializeObject(testCase);
        var clone = JsonConvert.DeserializeObject<TestCase>(json)!;

        InjectIntoApi(clone.Api, store);

        if (clone.Mock?.Interactions != null)
            foreach (var interaction in clone.Mock.Interactions)
                InjectIntoInteraction(interaction, store);

        return clone;
    }

    private static void InjectIntoApi(TestApi api, VariableStore store)
    {
        api.Request.Path    = InjectIntoString(api.Request.Path, store);
        api.Request.Params  = InjectIntoDictionary(api.Request.Params, store);
        api.Request.Headers = InjectIntoDictionary(api.Request.Headers, store);
        api.Request.Body    = InjectIntoJToken(api.Request.Body, store);
        api.Response.Body    = InjectIntoJToken(api.Response.Body, store);
        api.Response.Headers = InjectIntoDictionary(api.Response.Headers, store);
    }

    private static void InjectIntoInteraction(MockInteraction interaction, VariableStore store)
    {
        interaction.Request.Path    = InjectIntoString(interaction.Request.Path, store);
        interaction.Request.Params  = InjectIntoDictionary(interaction.Request.Params, store);
        interaction.Request.Headers = InjectIntoDictionary(interaction.Request.Headers, store);
        interaction.Request.Body    = InjectIntoJToken(interaction.Request.Body, store);
        interaction.Response.Body    = InjectIntoJToken(interaction.Response.Body, store);
        interaction.Response.Headers = InjectIntoDictionary(interaction.Response.Headers, store);
    }

    private static string? InjectIntoString(string? value, VariableStore store)
    {
        if (value == null || (!value.Contains("{{") && !value.Contains("${")))
            return value;

        return CombinedPattern.Replace(value, match => ResolveToString(match, store));
    }

    private static Dictionary<string, string>? InjectIntoDictionary(
        Dictionary<string, string>? dict, VariableStore store)
    {
        if (dict == null) return null;
        return dict.ToDictionary(kvp => kvp.Key, kvp => InjectIntoString(kvp.Value, store) ?? kvp.Value);
    }

    private static JToken? InjectIntoJToken(JToken? token, VariableStore store)
    {
        if (token == null) return null;
        InjectIntoJTokenInPlace(token, store);
        return token;
    }

    private static void InjectIntoJTokenInPlace(JToken token, VariableStore store)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var prop in obj.Properties().ToList())
                    ReplacePropertyValue(prop, store);
                break;
            case JArray arr:
                for (var i = 0; i < arr.Count; i++)
                    ReplaceArrayElement(arr, i, store);
                break;
        }
    }

    private static void ReplacePropertyValue(JProperty prop, VariableStore store)
    {
        if (prop.Value is JValue { Type: JTokenType.String } strVal)
        {
            var str = strVal.Value<string>()!;
            var exactMatch = ExactRuntimePattern.Match(str);
            if (exactMatch.Success)
                prop.Value = ResolveToJToken(exactMatch.Groups[1].Value, store);
            else if (str.Contains("{{") || str.Contains("${"))
                prop.Value = new JValue(InjectIntoString(str, store));
        }
        else
            InjectIntoJTokenInPlace(prop.Value, store);
    }

    private static void ReplaceArrayElement(JArray arr, int index, VariableStore store)
    {
        if (arr[index] is JValue { Type: JTokenType.String } strVal)
        {
            var str = strVal.Value<string>()!;
            var exactMatch = ExactRuntimePattern.Match(str);
            if (exactMatch.Success)
                arr[index] = ResolveToJToken(exactMatch.Groups[1].Value, store);
            else if (str.Contains("{{") || str.Contains("${"))
                arr[index] = new JValue(InjectIntoString(str, store));
        }
        else
            InjectIntoJTokenInPlace(arr[index], store);
    }

    private static JToken ResolveToJToken(string reference, VariableStore store)
    {
        var dot = reference.IndexOf('.');
        return dot > 0
            ? store.Resolve(reference[..dot], reference[(dot + 1)..])
            : store.Resolve(reference);
    }

    private static string ResolveToString(Match match, VariableStore store)
    {
        if (match.Groups[1].Success)
            return ResolveToJToken(match.Groups[1].Value, store).ToString();

        var envVar = match.Groups[2].Value;
        return Environment.GetEnvironmentVariable(envVar)
               ?? throw UndefinedVariableException.ForEnvironmentVariable(envVar);
    }
}

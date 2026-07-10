using System.Text.RegularExpressions;
using ConfIT.Model;
using JsonDiffPatchDotNet;

namespace ConfIT.Matching;

public static class ResultMatcher
{
    private const string Wildcard = "*";

    public static MatchResult MatchResponseBody(
        JToken actualResponse,
        JToken? expectedResponse,
        Matcher? matcher,
        IReadOnlyDictionary<string, SemanticMatcherFunc>? customMatchers = null)
    {
        var actual          = actualResponse.DeepClone();
        var semanticFailure = SemanticMatcher.Apply(actual, expectedResponse, matcher?.Semantic, customMatchers);
        if (semanticFailure is not null)
            return new MatchResult(false, semanticFailure);

        var response  = ApplyMatcher(actual, matcher);
        expectedResponse = ApplyIgnoreMatcher(expectedResponse, matcher?.Ignore);
        var diff      = new JsonDiffPatch().Diff(response, expectedResponse);
        if (diff is null)
            return MatchResult.Ok;

        return new MatchResult(false, DeltaFormatter.Format(diff));
    }

    private static JToken? ApplyMatcher(JToken response, Matcher? matcher)
    {
        if (matcher is null) return response;

        response = ApplyPatternMatcher(response, matcher.Pattern);
        response = ApplyIgnoreMatcher(response, matcher.Ignore);
        return response;
    }

    private static JToken? ApplyPatternMatcher(JToken response, Dictionary<string, string>? patterns)
    {
        if (patterns is { Count: > 0 })
            foreach (var (keyWithParent, regex) in patterns)
            {
                var (key, parents) = ExtractKeyAndParentPath(keyWithParent);
                RemoveField(response, key, parents, regex);
            }

        return response;
    }

    private static JToken? ApplyIgnoreMatcher(JToken? response, List<string>? ignore)
    {
        if (response is not null && ignore is { Count: > 0 })
            foreach (var keyWithParent in ignore)
            {
                var (key, parents) = ExtractKeyAndParentPath(keyWithParent);
                RemoveField(response, key, parents);
            }

        return response;
    }

    private static void RemoveField(this JToken token, string key, string parentsKey, string? regex = null)
    {
        if (token is not JContainer container) return;

        var removeList = new List<JToken>();
        foreach (var el in container.Children())
        {
            if (el is JProperty p && key.Equals(p.Name) && IsParentMatching(p, parentsKey))
                if (string.IsNullOrWhiteSpace(regex) || Regex.IsMatch(p.Value.ToString(), regex))
                    removeList.Add(el);

            el.RemoveField(key, parentsKey, regex);
        }

        foreach (var el in removeList)
            el.Remove();
    }

    private static bool IsParentMatching(JProperty prop, string parentsKey) =>
        // Empty parentsKey must keep matching any parent unconditionally — this short-circuit
        // must stay first and must not be folded into the normalized comparison below.
        string.IsNullOrWhiteSpace(parentsKey)
        || NormalizeArrayIndices(prop.Parent!.Path).Equals(BuildParentTemplate(parentsKey));

    private static string NormalizeArrayIndices(string path) =>
        Regex.Replace(path, @"\[\d+\]", "[*]");

    private static string BuildParentTemplate(string parentsKey)
    {
        var template = "";
        foreach (var segment in parentsKey.Split('.'))
            template += segment == Wildcard ? "[*]" : (template.Length > 0 ? "." : "") + segment;

        return template;
    }

    private static (string key, string parents) ExtractKeyAndParentPath(string keyWithParents)
    {
        var parts = keyWithParents.Split("__").ToList();
        var key   = parts.Last();
        parts.RemoveAt(parts.Count - 1);
        return (key, string.Join('.', parts));
    }
}

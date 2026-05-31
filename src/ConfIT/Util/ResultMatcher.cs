using System.Text.RegularExpressions;
using ConfIT.Server.Dto;
using FluentAssertions;
using JsonDiffPatchDotNet;

namespace ConfIT.Util;

public static class ResultMatcher
{
    public static void MatchResponseBody(
        JToken actualResponse,
        JToken expectedResponse,
        Matcher matcher,
        IReadOnlyDictionary<string, SemanticMatcherFunc>? customMatchers = null)
    {
        var actual = actualResponse.DeepClone();
        SemanticMatcher.Apply(actual, expectedResponse, matcher?.Semantic, customMatchers);
        var response = ApplyMatcher(actual, matcher);
        expectedResponse = ApplyIgnoreMatcher(expectedResponse, matcher?.Ignore);
        var diff = new JsonDiffPatch().Diff(response, expectedResponse);
        diff?.ToString().Should().BeNullOrWhiteSpace();
    }

    private static JToken ApplyMatcher(JToken response, Matcher matcher)
    {
        if (response is null || matcher is null)
            return response;

        response = ApplyPatternMatcher(response, matcher.Pattern);
        response = ApplyIgnoreMatcher(response, matcher.Ignore);

        return response;
    }

    private static JToken ApplyPatternMatcher(JToken response, Dictionary<string, string>? patterns)
    {
        if (patterns is { Count: > 0 })
            foreach (var (keyWithParent, regex) in patterns)
            {
                var (key, parents) = ExtractKeyAndParentPath(keyWithParent);
                RemoveField(response, key, parents, regex);
            }

        return response;
    }

    private static JToken ApplyIgnoreMatcher(JToken response, List<string>? ignore)
    {
        if (ignore is { Count: > 0 })
            foreach (var keyWithParent in ignore)
            {
                var (key, parents) = ExtractKeyAndParentPath(keyWithParent);
                RemoveField(response, key, parents);
            }

        return response;
    }

    private static void RemoveField(this JToken token, string key, string parentsKey, string regex = null)
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
        string.IsNullOrWhiteSpace(parentsKey) || prop.Parent.Path.Equals(parentsKey);

    private static (string key, string parents) ExtractKeyAndParentPath(string keyWithParents)
    {
        var parts = keyWithParents.Split("__").ToList();
        var key = parts.Last();
        parts.RemoveAt(parts.Count - 1);
        return (key, string.Join('.', parts));
    }
}

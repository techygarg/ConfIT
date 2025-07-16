using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ConfIT.Extension;
using ConfIT.Server.Dto;
using FluentAssertions;
using JsonDiffPatchDotNet;
using Newtonsoft.Json.Linq;

namespace ConfIT.Util
{
    public static class ResultMatcher
    {
        public static void MatchResponseBody(JToken actualResponse, JToken expectedResponse, Matcher matcher)
        {
            var response = ApplyMatcher(actualResponse.DeepClone(), matcher);
            expectedResponse = ApplyIgnoreMatcher(expectedResponse, matcher?.Ignore);
            var diff = new JsonDiffPatch().Diff(response, expectedResponse);
            diff?.ToString().Should().BeNullOrWhiteSpace();
        }

        private static JToken ApplyMatcher(JToken response, Matcher matcher)
        {
            if (response is null || matcher is null)
                return response;

            response = ApplyPatternMatcher(response, matcher?.Pattern);
            response = ApplyIgnoreMatcher(response, matcher?.Ignore);

            return response;
        }

        private static JToken ApplyPatternMatcher(JToken response, Dictionary<string, string> patterns)
        {
            if (patterns is { Count: > 0 })
                foreach (var (keyWithParent, regex) in patterns)
                {
                    var (key, parents) = ExtractKeyAndParentPath(keyWithParent);
                    RemoveField(response, key, parents, regex);
                }

            return response;
        }

        private static JToken ApplyIgnoreMatcher(JToken response, List<string> ignore)
        {
            if (ignore != null)
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
                    if (regex.IsNullOrWhiteSpace() || Regex.IsMatch(p.Value.ToString(), regex))
                        removeList.Add(el);

                el.RemoveField(key, parentsKey, regex);
            }

            foreach (var el in removeList)
                el.Remove();
        }

        private static bool IsParentMatching(JProperty prop, string parentsKey)
        {
            return parentsKey.IsNullOrWhiteSpace()
                   || prop.Parent.Path.Equals(parentsKey);
        }

        private static (string, string) ExtractKeyAndParentPath(string keyWithPatents)
        {
            var parentsAndKey = keyWithPatents.Split("__").ToList();
            var key = parentsAndKey.Last();
            parentsAndKey.RemoveAt(parentsAndKey.Count() - 1);
            return (key, string.Join('.', parentsAndKey));
        }
    }
}
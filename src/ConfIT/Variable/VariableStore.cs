using System.Collections.Concurrent;
using System.Linq;
using ConfIT.Variable.Exception;
using Newtonsoft.Json.Linq;

namespace ConfIT.Variable
{
    public sealed class VariableStore
    {
        public static readonly VariableStore Instance = new();

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, JToken>> _store = new();

        public VariableStore() { }

        public void Set(string testName, string varName, JToken value)
        {
            var bucket = _store.GetOrAdd(testName, _ => new ConcurrentDictionary<string, JToken>());
            if (!bucket.TryAdd(varName, value))
                throw new VariableCollisionException(testName, varName);
        }

        public JToken Resolve(string varName)
        {
            var matches = _store
                .Where(outer => outer.Value.ContainsKey(varName))
                .Select(outer => (testName: outer.Key, value: outer.Value[varName]))
                .ToList();

            return matches.Count switch
            {
                0 => throw UndefinedVariableException.ForShortName(varName),
                1 => matches[0].value,
                _ => throw new AmbiguousVariableException(varName, matches.Select(m => m.testName))
            };
        }

        public JToken Resolve(string testName, string varName)
        {
            if (_store.TryGetValue(testName, out var bucket) && bucket.TryGetValue(varName, out var value))
                return value;
            throw UndefinedVariableException.ForFullPrefix(testName, varName);
        }
    }
}

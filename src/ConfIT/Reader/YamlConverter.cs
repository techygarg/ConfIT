using System.Globalization;
using Newtonsoft.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace ConfIT.Reader;

internal static class YamlConverter
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNodeTypeResolver(new ScalarTypeResolver())
        .Build();

    internal static JObject ToJObject(string yaml)
    {
        var graph = Deserializer.Deserialize<object>(yaml);
        var json = JsonConvert.SerializeObject(graph);
        return JObject.Parse(json);
    }

    // Infers .NET types from unquoted YAML scalars so that integers, booleans, and
    // floats survive the round-trip to JObject with correct JTokenType values.
    // Quoted scalars (ScalarStyle != Plain) are always strings — intentional.
    private sealed class ScalarTypeResolver : INodeTypeResolver
    {
        public bool Resolve(NodeEvent nodeEvent, ref Type currentType)
        {
            if (nodeEvent is not Scalar { Style: ScalarStyle.Plain } scalar) return false;
            if (currentType != typeof(object)) return false;

            var v = scalar.Value;

            if (v is "true" or "True" or "TRUE" or "false" or "False" or "FALSE")
            {
                currentType = typeof(bool);
                return true;
            }

            if (long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                currentType = typeof(long);
                return true;
            }

            if (double.TryParse(v, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                    out _))
            {
                currentType = typeof(double);
                return true;
            }

            return false;
        }
    }
}

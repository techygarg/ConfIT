using System.IO;
using static System.IO.Path;

namespace ConfIT.Model;

public abstract class BaseRequestResponse
{
    public string BodyFromFile { get; set; }
    public JToken Body { get; set; }
    public JToken Override { get; set; }
    public Dictionary<string, string> Headers { get; set; }

    public virtual void Initialize(string folder)
    {
        if (string.IsNullOrWhiteSpace(BodyFromFile)) return;

        var payload = JToken.Parse(File.ReadAllText(GetFullPath($"{folder}/{BodyFromFile}")));
        ApplyOverride(payload);
        Body = payload;
    }

    private void ApplyOverride(JToken payload)
    {
        if (Override is null) return;

        if (payload is JArray array)
            foreach (var item in array.OfType<JObject>())
                item.Merge(Override);
        else if (payload is JObject obj)
            obj.Merge(Override);
    }
}

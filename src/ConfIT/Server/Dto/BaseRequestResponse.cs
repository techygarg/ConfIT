using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using static System.IO.Path;
using ConfIT.Extension;

namespace ConfIT.Server.Dto
{
    public abstract class BaseRequestResponse
    {
        public string BodyFromFile { get; set; }
        public JToken Body { get; set; }
        public JToken Override { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public virtual void Initialize(string folder)
        {
            if (!BodyFromFile.IsNullOrWhiteSpace())
            {
                var payload = JToken.Parse(File.ReadAllText(GetFullPath($"{folder}/{BodyFromFile}")));
                if (Override != null)
                {
                    if (payload.Type == JTokenType.Array)
                    {
                        foreach (var item in payload)
                            if (item.Type == JTokenType.Object)
                                (item as JObject)?.Merge(Override);
                    }
                    else if (payload.Type == JTokenType.Object)
                        (payload as JObject)?.Merge(Override);

                    Body = payload;
                }
                else
                    Body = payload;
            }
        }
    }
}
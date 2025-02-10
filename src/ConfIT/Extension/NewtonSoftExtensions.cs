using ConfIT.Server.Dto;
using Newtonsoft.Json.Linq;

namespace ConfIT.Extension
{
    public static class NewtonSoftExtensions
    {
        public static TestCase ToTestCase(this JToken jToken, string requestFolder, string responseFolder) =>
            jToken?.ToObject<TestCase>().Initialize(requestFolder, responseFolder);

    }
}
using ConfIT.Server.Dto;

namespace ConfIT.Extension;

public static class JTokenExtensions
{
    public static TestCase ToTestCase(this JToken jToken, string requestFolder, string responseFolder) =>
        jToken?.ToObject<TestCase>()?.Initialize(requestFolder, responseFolder)!;
}

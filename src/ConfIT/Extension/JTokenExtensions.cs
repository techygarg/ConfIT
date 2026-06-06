using ConfIT.Model;

namespace ConfIT.Extension;

public static class JTokenExtensions
{
    public static TestCase ToTestCase(this JToken jToken, string requestFolder, string responseFolder)
    {
        return jToken?.ToObject<TestCase>()?.Initialize(requestFolder, responseFolder)!;
    }
}

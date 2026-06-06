using ConfIT.Model;
using ConfIT.Reader;

namespace ConfIT.Extension;

public static class JTokenExtensions
{
    public static TestCase ToTestCase(this JToken jToken, string? requestFolder, string? responseFolder)
    {
        var raw = jToken?.ToObject<TestCase>();
        return raw is null ? null! : TestCaseResolver.Resolve(raw, requestFolder, responseFolder);
    }
}

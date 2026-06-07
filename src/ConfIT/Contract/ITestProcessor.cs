using ConfIT.Model;

namespace ConfIT.Contract;

public interface ITestProcessor
{
    void Before(TestApi testApi);
    void After(TestApi testApi, JToken actualResponse);
}

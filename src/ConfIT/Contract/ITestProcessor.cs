using ConfIT.Server.Dto;
using Newtonsoft.Json.Linq;

namespace ConfIT.Contract
{
    public interface ITestProcessor
    {
        void Before(TestApi testApi);
        void After(TestApi testApi, JToken actualResponse);
    }
}
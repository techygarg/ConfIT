using System;
using ConfIT.Contract;
using ConfIT.Server.Dto;
using Newtonsoft.Json.Linq;

namespace User.ComponentTests.SetUp.TestProcessor
{
    /// <summary>
    /// We can implement ITestProcessor, which will be created by TestProcessFactory for matching testApi name.
    /// We can perform required before and after setup. cleanup tasks 
    /// </summary>
    public class ShouldCreteAUserTestProcessor : ITestProcessor
    {
        public void Before(TestApi testApi)
        {
            Console.WriteLine("ShouldCreteAUserTestProcessor --> Before invoked");
        }

        public void After(TestApi testApi, JToken actualResponse)
        {
            Console.WriteLine("ShouldCreteAUserTestProcessor --> After invoked");
        }
    }
}
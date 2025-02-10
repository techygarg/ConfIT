namespace ConfIT.Contract
{
    public interface ITestProcessorFactory
    {
        ITestProcessor GetTestProcessor(string testName);
    }
}
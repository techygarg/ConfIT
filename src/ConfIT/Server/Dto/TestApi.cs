namespace ConfIT.Server.Dto
{
    public class TestApi
    {
        public HttpTestRequest Request { get; set; }
        public HttpTestResponse Response { get; set; }

        public TestApi Initialize(string requestFolder, string responseFolder)
        {
            Request.Initialize(requestFolder);
            Response.Initialize(responseFolder);
            return this;
        }
    }
}
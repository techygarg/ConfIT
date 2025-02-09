namespace ConfIT.Server.Dto
{
    public abstract class ApiInteraction
    {
        public HttpTestRequest Request { get; set; }
        public HttpTestResponse Response { get; set; }

        public ApiInteraction Initialize(string requestFolder, string responseFolder)
        {
            Request.Initialize(requestFolder);
            Response.Initialize(responseFolder);
            return this;
        }
    }
}
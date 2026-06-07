namespace ConfIT.Model;

public abstract class ApiInteraction
{
    public HttpTestRequest Request { get; set; }
    public HttpTestResponse Response { get; set; }
}

namespace User.Api.Error.Exceptions
{
    public class NotFoundException : System.Exception
    {
        public NotFoundException(string message, System.Exception ex = null) : base(message, ex)
        {
        }
    }
}
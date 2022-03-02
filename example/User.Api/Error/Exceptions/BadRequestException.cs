namespace User.Api.Error.Exceptions
{
    public class BadRequestException : System.Exception
    {
        public BadRequestException(string message, System.Exception ex = null) : base(message, ex)
        {
        }
    }
}
using ConfIT.Contract;

namespace User.IntegrationTests
{
    public class AuthTokenProvider : IAuthTokenProvider
    {
        public string Token()
        {
            return "Bearer Have Your Token Provider Implementation Here.";
        }
    }
}
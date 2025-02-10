using System.Threading.Tasks;

namespace User.Api.Operation.Provider
{
    public interface IJustAnotherServiceProvider
    {
        public Task<EmailResponse> VerifyByGet(string email);
        public Task<EmailResponse> VerifyByPost(string email);
        public Task<EmailResponse> VerifyByGetQuery(string email);
    }
}
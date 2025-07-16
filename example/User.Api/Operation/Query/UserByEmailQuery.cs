using MediatR;
using User.Api.Operation.Response;

namespace User.Api.Operation.Query
{
    public class UserByEmailQuery : IRequest<UserResponse>
    {
        public string EmailId { get; set; }
    }
}
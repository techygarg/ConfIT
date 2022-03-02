using MediatR;
using User.Api.Operation.Response;

namespace User.Api.Operation.Query
{
    public class UserByIdQuery : IRequest<UserResponse>
    {
        public int Id { get; set; }
    }
}
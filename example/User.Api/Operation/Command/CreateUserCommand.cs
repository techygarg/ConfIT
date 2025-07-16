using MediatR;
using User.Api.Operation.Response;

namespace User.Api.Operation.Command
{
    public class CreateUserCommand : IRequest<UserCreatedResponse>
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public int Age { get; set; }
    }
}
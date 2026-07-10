using System.Threading.Tasks;
using MediatR;
using User.Api.Operation.Command;
using User.Api.Operation.Response;

namespace User.Api.GraphQL
{
    public class Mutation
    {
        public async Task<UserCreatedResponse> CreateUser(CreateUserCommand input, IMediator mediator) =>
            await mediator.Send(input);
    }
}

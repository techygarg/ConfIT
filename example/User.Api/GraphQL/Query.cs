using System.Threading.Tasks;
using MediatR;
using User.Api.Operation.Query;
using User.Api.Operation.Response;

namespace User.Api.GraphQL
{
    public class Query
    {
        public async Task<UserResponse> UserById(int id, IMediator mediator) =>
            await mediator.Send(new UserByIdQuery { Id = id });
    }
}

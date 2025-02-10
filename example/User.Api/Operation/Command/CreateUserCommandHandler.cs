using System.Threading;
using System.Threading.Tasks;
using MediatR;
using User.Api.Error.Exceptions;
using User.Api.Operation.Provider;
using User.Api.Operation.Response;
using User.Api.Persistence;

namespace User.Api.Operation.Command
{
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserCreatedResponse>
    {
        private readonly UserDbContext _context;
        private readonly IJustAnotherServiceProvider _anotherServiceProvider;

        public CreateUserCommandHandler(UserDbContext context, IJustAnotherServiceProvider anotherServiceProvider)
        {
            _context = context;
            _anotherServiceProvider = anotherServiceProvider;
        }

        public async Task<UserCreatedResponse> Handle(CreateUserCommand command, CancellationToken cancellationToken)
        {
            // Below all three calls to cover dependencies on other service. We will mock these calls in 
            // in our component tests. We will call actual service in our integration tests.
            // For dem purpose, below calls doing the same thing, but with different types of HTTP calls
            // Get with path, Get with query parameter, Post with payload. 
            // Please refer mocks section in example/User.ComponentTests/TestCase/user.json
            var isValid1 = await _anotherServiceProvider.VerifyByGet(command.Email);
            var isValid2 = await _anotherServiceProvider.VerifyByPost(command.Email);
            var isValid3 = await _anotherServiceProvider.VerifyByGetQuery(command.Email);
            if (!isValid1.IsValid || !isValid2.IsValid || !isValid3.IsValid)
                throw new BadRequestException("Invalid Email.");

            var user = new Users { Name = command.Name, Email = command.Email, Age = command.Age };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            return new UserCreatedResponse { Id = user.Id };
        }
    }
}
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using User.Api.Error.Exceptions;
using User.Api.Operation.Response;
using User.Api.Persistence;

namespace User.Api.Operation.Query
{
    public class UserByEmailQueryHandler : IRequestHandler<UserByEmailQuery, UserResponse>
    {
        private readonly UserDbContext _context;

        public UserByEmailQueryHandler(UserDbContext context)
        {
            _context = context;
        }

        public async Task<UserResponse> Handle(UserByEmailQuery request, CancellationToken cancellationToken)
        {
            var dao = await _context.Users.FirstOrDefaultAsync(x => x.Email.Equals(request.EmailId), cancellationToken);
            if (dao == null)
                throw new NotFoundException($"User doesn't exist with email : {request.EmailId}");

            return new UserResponse
            {
                Id = dao.Id,
                Name = dao.Name,
                Email = dao.Email,
                Age = dao.Age,
            };
        }
    }
}
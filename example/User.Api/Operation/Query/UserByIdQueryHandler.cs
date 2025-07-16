using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using User.Api.Error.Exceptions;
using User.Api.Operation.Response;
using User.Api.Persistence;

namespace User.Api.Operation.Query
{
    public class UserByIdQueryHandler : IRequestHandler<UserByIdQuery, UserResponse>
    {
        private readonly UserDbContext _context;

        public UserByIdQueryHandler(UserDbContext context)
        {
            _context = context;
        }


        public async Task<UserResponse> Handle(UserByIdQuery request, CancellationToken cancellationToken)
        {
            var dao = await _context.Users.FirstOrDefaultAsync(x => x.Id.Equals(request.Id), cancellationToken);
            if (dao == null)
                throw new NotFoundException($"User doesn't exist with Id : {request.Id}");

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
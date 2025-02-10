using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using User.Api.Operation.Command;
using User.Api.Operation.Query;

namespace User.Api.Controller
{
    public class UserController : ApiBaseController
    {
        private readonly IMediator _mediator;

        public UserController(IMediator mediator) =>
            _mediator = mediator;

        [HttpGet("{email}")]
        public async Task<IActionResult> UserByEmail([FromRoute, Required] string email) =>
            Ok(await _mediator.Send(new UserByEmailQuery { EmailId = email }));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> UserById([FromRoute, Required] int id) =>
            Ok(await _mediator.Send(new UserByIdQuery { Id = id }));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserCommand user) =>
            CreatedResult(await _mediator.Send(user));
    }
}
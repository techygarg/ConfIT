using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace User.Api.Controller
{
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    [Route("api/[Controller]")]
    public abstract class ApiBaseController : ControllerBase
    {
        [NonAction]
        protected ObjectResult CreatedResult([ActionResultObjectValue] object value)
            => new(value) { StatusCode = StatusCodes.Status201Created };
    }
}
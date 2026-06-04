using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace User.Api.Controller
{
    public class AuthController : ApiBaseController
    {
        [HttpGet("verify")]
        public IActionResult Verify() =>
            Ok(new { authorization = Request.Headers["Authorization"].FirstOrDefault() ?? string.Empty });
    }
}

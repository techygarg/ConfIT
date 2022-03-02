using Microsoft.AspNetCore.Mvc;

namespace JustAnotherService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DemoController : ControllerBase
    {
        [HttpGet("{email}")]
        public EmailResponse Get(string email) =>
            new() { IsValid = email.EndsWith(".com") };
        
        [HttpGet]
        public EmailResponse GetWithQueryString([FromQuery] string email) =>
            new() { IsValid = email.EndsWith(".com") };

        [HttpPost]
        public EmailResponse Post(EmailContainer container) =>
            new() { IsValid = container.Email.EndsWith(".com") };
    }

    public class EmailResponse
    {
        public bool IsValid { get; set; }
    }
}
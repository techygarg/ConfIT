using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using User.Api.Operation.Response;

namespace User.Api.Controller
{
    [ApiController]
    public class SomeMoreExamplesController : ControllerBase
    {
        [HttpGet("api/examples/1")]
        public IActionResult Example1() =>
            Ok(new List<UserResponse>()
            {
                new() { Id = 11, Age = 11, Email = "eleven@gmail.com", Name = "Eleven" },
                new() { Id = 12, Age = 12, Email = "telve@gmail.com", Name = "Twelve" },
                new() { Id = 13, Age = 13, Email = "thirteen@gmail.com", Name = "Thirteen" },
                new() { Id = 14, Age = 14, Email = "fourteen@gmail.com", Name = "Fourteen" }
            });

        [HttpGet("api/examples/2")]
        public IActionResult Example2() =>
            Ok(new List<UserResponse>
            {
                new() { Id = 11, Age = 20, Email = "eleven@gmail.com", Name = "Eleven" },
                new() { Id = 12, Age = 20, Email = "telve@gmail.com", Name = "Twelve" },
                new() { Id = 13, Age = 20, Email = "thirteen@gmail.com", Name = "Thirteen" },
                new() { Id = 14, Age = 20, Email = "fourteen@gmail.com", Name = "Fourteen" }
            });

        [HttpGet("api/examples/3")]
        public IActionResult Example3() =>
            Ok(new
            {
                id = 1,
                child1 = new { email = "child1@gmail.com", age = 20, child2 = new { name = "test-3", pincode = 989898 } },
                child3 = new { email = "child3@gmail.com", age = 30, child4 = new { name = "test-4", pincode = 676767 } },
                child5 = new { email = "child5@gmail.com", age = 40 },
            });

        [HttpGet("api/examples/4")]
        public IActionResult Example4() =>
            Ok(new
            {
                id = 1,
                child1 = new { email = "child1@gmail.com", age = 20, child2 = new { name = "test-3", pincode = 989898, extra = "extra1" } },
                child3 = new { email = "child3@gmail.com", age = 30, child4 = new { name = "test-4", pincode = 676767, extra = "extra2" } },
                child5 = new { email = "child5@gmail.com", age = 40 },
            });
    }
}
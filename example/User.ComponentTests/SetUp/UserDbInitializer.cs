using System.Collections.Generic;
using System.Linq;
using User.Api.Persistence;

namespace User.ComponentTests.SetUp
{
    /// <summary>
    /// Reference data inserted once, before any test runs, from the fixture's onStarted hook.
    ///
    /// This is the place for data that must exist before the suite starts — lookup tables,
    /// a tenant row, a fixed admin account. Data a single test needs should be created by
    /// that test over HTTP instead, so the test stays readable and self-contained: the test
    /// definitions in TestCase/ create every user they assert on.
    /// </summary>
    public class UserDbInitializer
    {
        private readonly UserDbContext _context;

        public UserDbInitializer(UserDbContext context) =>
            _context = context;

        public void Seed()
        {
            // UserDbContext creates a fresh schema once per process, so this runs against an
            // empty database — but guard anyway, so seeding stays safe to call more than once.
            if (_context.Users.Any()) return;

            _context.AddRange(new List<Users>
            {
                new() { Id = 1, Name = "Seed User One", Email = "seed-user-1@test.com", Age = 41 },
                new() { Id = 2, Name = "Seed User Two", Email = "seed-user-2@test.com", Age = 42 }
            });
            _context.SaveChanges();
        }
    }
}

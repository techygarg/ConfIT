using System.Collections.Generic;
using User.Api.Persistence;

namespace User.ComponentTests.SetUp
{
    /// <summary>
    /// Add any initial data set-up required for testApi suite. It will runs only one during all tests runs
    /// </summary>
    public class UserDbInitializer
    {
        private readonly UserDbContext _context;

        public UserDbInitializer(UserDbContext context) =>
            _context = context;

        public void Seed()
        {
            //SeedUsers(_context);
        }

        private static void SeedUsers(UserDbContext context)
        {
            var data = new List<Users>
            {
                new() { Name = "User1", Id = 1, Age = 10, Email = "testApi@testApi.com" },
                new() { Name = "User2", Id = 2, Age = 20, Email = "test1@testApi.com" },
            };
            context.AddRange(data);
            context.SaveChanges();
        }
    }
}
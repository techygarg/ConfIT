using System;
using ConfIT;
using Microsoft.Extensions.DependencyInjection;
using User.Api;
using User.Api.Persistence;

namespace User.ComponentTests.SetUp
{
    public class TestSuiteFixture : IDisposable
    {
        private readonly BootstrappedSuite _suite;

        public TestSuiteFixture() =>
            _suite = SuiteBootstrapper.ForComponent<Startup>("suite.config.yaml",
                onStarted: services =>
                {
                    using var scope = services.CreateScope();
                    var dbContext   = scope.ServiceProvider.GetRequiredService<UserDbContext>();
                    new UserDbInitializer(dbContext).Seed();
                });

        public TestSuiteContext Context  => _suite.Context;

        public void Dispose() => _suite.Dispose();
    }
}

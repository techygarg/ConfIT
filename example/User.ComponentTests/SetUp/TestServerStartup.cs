using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using User.Api;
using User.Api.Error;
using User.Api.Persistence;

namespace User.ComponentTests.SetUp
{
    /// <summary>
    /// We can override default behaviour of actual api start up class in case we need to set up something custom 
    /// </summary>
    public class TestServerStartup : Startup, IDisposable
    {
        public TestServerStartup(IConfiguration configuration, IWebHostEnvironment env)
            : base(configuration, env)
        {
        }


        protected override void AddDbContexts(IServiceCollection services)
        {
            services.AddDbContext<UserDbContext>(
                options => options.UseInMemoryDatabase("UserDb"));
        }

        protected override void AddMvcServices(IServiceCollection services)
        {
            services.AddMvcCore(options => { options.Filters.Add(typeof(UserExceptionFilterAttribute)); })
                .AddApplicationPart(typeof(Startup).Assembly);
        }

        public void Dispose()
        {
        }
    }
}
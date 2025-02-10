using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using User.Api.Extension;
using User.Api.Persistence;

namespace User.Api
{
    public class Startup
    {
        public const string AppName = "User API";
        public const string AppVersion = "v1";
        private static IConfiguration Configuration { get; set; }
        private IWebHostEnvironment HostingEnvironment { get; }

        public Startup(IConfiguration configuration, IWebHostEnvironment environment)
        {
            Configuration = configuration;
            HostingEnvironment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                .ConfigureApiBehaviorOptions(options => { options.SuppressModelStateInvalidFilter = true; })
                .AddNewtonsoftJson(options => { options.AllowInputFormatterExceptionMessages = false; });

            services
                .AddDependencies(Configuration)
                .AddApiSettings(Configuration);

            AddMvcServices(services);
            AddDbContexts(services);
        }


        /// <summary>
        /// This method is defined as virtual so that we can override default behaviour
        /// in component tests if required to change default db context  
        /// </summary>
        /// <param name="services"></param>
        protected virtual void AddDbContexts(IServiceCollection services) =>
            services
                .AddDbContext<UserDbContext>(opt =>
                    opt.UseSqlite(@"Data Source=User.db"));

        /// <summary>
        /// This method is defined as virtual so that we can override default behaviour
        /// in component tests if required to change default MVC services set up
        /// </summary>
        /// <param name="services"></param>
        protected virtual void AddMvcServices(IServiceCollection services) =>
            services.AddMvcServices(Configuration);

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app
                .UseHttpsRedirection()
                .UseStaticFiles()
                .UseRouting()
                .UseAuthentication()
                .UseAuthorization()
                .UseEndpoints(endpoints => { endpoints.MapDefaultControllerRoute(); });
        }

        public static bool IsLocalComponentTestsRunning(IConfiguration configuration) =>
            configuration.GetValue("IsLocalComponentTests", false);
    }
}
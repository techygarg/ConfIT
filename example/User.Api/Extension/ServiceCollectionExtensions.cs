using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using User.Api.Error;

namespace User.Api.Extension
{
    [ExcludeFromCodeCoverage]
    internal static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMvcServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddMvcCore(options => { options.Filters.Add(typeof(UserExceptionFilterAttribute)); })
                .AddApiExplorer();

            return services;
        }
    }
}
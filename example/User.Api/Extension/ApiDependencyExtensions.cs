using System.Diagnostics.CodeAnalysis;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using User.Api.Operation.Provider;

namespace User.Api.Extension
{
    [ExcludeFromCodeCoverage]
    internal static class ApiDependencyExtensions
    {
        public static IServiceCollection AddDependencies(this IServiceCollection services, IConfiguration configuration)
        {
            return services
                .AddHttpContextAccessor()
                .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Startup).Assembly))
                .AddScoped<IJustAnotherServiceProvider, JustAnotherServiceProvider>();
        }
    }
}
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using User.Api.Operation.Provider;

namespace User.Api.Extension
{
    internal static class ApiSettingsExtensions
    {
        public static IServiceCollection AddApiSettings(this IServiceCollection services, IConfiguration configuration)
        {
           return services
               .AddSetting<JustAnotherServiceProviderSettings>(configuration, "JustAnotherService");
        }

        private static IServiceCollection AddSetting<TConfig>(this IServiceCollection services, IConfiguration configuration,
            string key) where TConfig : class, new()
        {
            services.Configure<TConfig>(configuration.GetSection(key))
                .AddSingleton(x => x.GetService<IOptions<TConfig>>().Value);

            return services;
        }

        private static IServiceCollection AddSetting<TConfig>(this IServiceCollection services, IConfiguration configuration,
            string key1, string key2) where TConfig : class, new()
        {
            services.Configure<TConfig>(configuration.GetSection(key1).GetSection(key2))
                .AddSingleton(x => x.GetService<IOptions<TConfig>>().Value);

            return services;
        }
    }
}
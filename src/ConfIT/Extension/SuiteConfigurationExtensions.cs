using ConfIT.Config;
using ConfIT.Config.AuthProvider;
using ConfIT.Contract;
using ConfIT.Runner.Boot;

namespace ConfIT.Extension;

public static class SuiteConfigurationExtensions
{
    public static SuiteConfig ToSuiteConfig(this ComponentConfig config)
    {
        return new SuiteConfig
        {
            ApiServerUrl = config.Api.Url ?? string.Empty,
            MockServerUrl = config.Mock?.Url ?? string.Empty,
            EnableMockServerLogs = config.Mock?.EnableLogs ?? false,
            ApiResponseFolder = config.Folders?.Response ?? string.Empty,
            RequestBodyFolder = config.Folders?.RequestBody ?? string.Empty,
            ResponseBodyFolder = config.Folders?.ResponseBody ?? string.Empty
        };
    }

    public static SuiteConfig ToSuiteConfig(this IntegrationConfig config)
    {
        return new SuiteConfig
        {
            ApiServerUrl = config.Api.Url ?? string.Empty,
            ApiResponseFolder = config.Folders?.Response ?? string.Empty,
            RequestBodyFolder = config.Folders?.RequestBody ?? string.Empty,
            ResponseBodyFolder = config.Folders?.ResponseBody ?? string.Empty
        };
    }

    public static TestFilter? ToTestFilter(this ComponentConfig config)
    {
        return BuildFilter(config.Filter);
    }

    public static TestFilter? ToTestFilter(this IntegrationConfig config)
    {
        return BuildFilter(config.Filter);
    }

    public static AppLauncherConfig ToAppLauncherConfig(this ComponentConfig config)
    {
        if (!config.Startup.IsCommand)
            throw new InvalidOperationException(
                $"ToAppLauncherConfig() requires startup.mode to be '{StartupConfig.CommandMode}', " +
                $"but it is '{config.Startup.Mode}'.");

        return new AppLauncherConfig
        {
            Command     = config.Startup.Command!,
            StopCommand = config.Startup.StopCommand,
            Readiness   = config.Startup.Readiness!,
            Env         = config.Startup.Env ?? new Dictionary<string, string>()
        };
    }

    public static IAuthTokenProvider? ToAuthTokenProvider(this ComponentConfig config) =>
        BuildAuthProvider(config.Auth);

    public static IAuthTokenProvider? ToAuthTokenProvider(this IntegrationConfig config) =>
        BuildAuthProvider(config.Auth);

    private static IAuthTokenProvider? BuildAuthProvider(AuthConfig? auth)
    {
        if (auth is null) return null;
        return auth.Type switch
        {
            "bearer"                    => new BearerAuthTokenProvider(auth.Token!, auth.HeaderKey),
            "oauth2-client-credentials" => new OAuth2ClientCredentialsProvider(auth),
            "api-key"                   => new ApiKeyAuthTokenProvider(auth.HeaderKey!, auth.Value!),
            _                           => throw new InvalidOperationException(
                                               $"Unknown auth type '{auth.Type}'.")
        };
    }

    private static TestFilter? BuildFilter(FilterConfig? filter)
    {
        if (filter is null) return null;
        return filter.Strategy == "tags"
            ? TestFilter.CreateForTagsFromEnvVariable(filter.EnvVariable!)
            : TestFilter.CreateForTestsFromEnvVariable(filter.EnvVariable!);
    }
}

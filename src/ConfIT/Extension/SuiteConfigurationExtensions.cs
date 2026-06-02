using ConfIT.Server.Boot;

namespace ConfIT.Extension;

public static class SuiteConfigurationExtensions
{
    public static SuiteConfig ToSuiteConfig(this ComponentConfig config)
    {
        return new SuiteConfig
        {
            ApiServerUrl = config.Api.Url ?? string.Empty,
            MockServerUrl = config.Mock?.Url ?? string.Empty,
            ApiResponseFolder = config.Folders?.Response ?? string.Empty,
            RequestBodyFolder = config.Folders?.RequestBody ?? string.Empty,
            ResponseBodyFolder = config.Folders?.ResponseBody ?? string.Empty
        };
    }

    public static SuiteConfig ToSuiteConfig(this IntegrationEnvironmentConfig config)
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

    public static TestFilter? ToTestFilter(this IntegrationEnvironmentConfig config)
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
            Command = config.Startup.Command!,
            Readiness = config.Startup.Readiness!,
            Env = config.Startup.Env ?? new Dictionary<string, string>()
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
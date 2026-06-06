using System.IO;
using System.Text.RegularExpressions;
using ConfIT.Config.AuthProvider;
using ConfIT.Reader;
using ConfIT.Runner.Boot;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;

namespace ConfIT.Config;

public static class SuiteConfiguration
{
    private const string TestEnvironmentKey = "TEST_ENVIRONMENT";

    #region Public API

    public static ComponentConfig LoadComponent(string filePath)
    {
        var root = ParseYaml(filePath);
        var section = RequireSection(root, "component", filePath);

        ResolveEnvVars(section, "component", filePath);
        var cfg = Deserialize<ComponentConfig>(section);

        ValidateComponent(cfg, filePath);
        return cfg;
    }

    public static IntegrationConfig LoadIntegration(string filePath, string? environment = null)
    {
        var root = ParseYaml(filePath);
        var integration = RequireSection(root, "integration", filePath);

        var activeEnv = environment
                        ?? Environment.GetEnvironmentVariable(TestEnvironmentKey)
                        ?? integration["default"]?.Value<string>()
                        ?? throw ConfigError(filePath,
                            $"Cannot determine active environment. Set {TestEnvironmentKey}, " +
                            "pass an environment argument, or add 'default: <name>' to the integration section.");

        var section = integration[activeEnv] as JObject
                      ?? throw ConfigError(filePath, $"No environment '{activeEnv}' found in the integration section");

        ResolveEnvVars(section, $"integration.{activeEnv}", filePath);
        var cfg = Deserialize<IntegrationConfig>(section);

        ValidateIntegrationEnv(cfg, activeEnv, filePath);
        return cfg;
    }

    #endregion

    #region Validation

    private static void ValidateComponent(ComponentConfig cfg, string filePath)
    {
        var mode = cfg.Startup?.Mode ?? StartupConfig.InProcessMode;
        Validate.OneOf(mode, "component.startup.mode", filePath, StartupConfig.InProcessMode,
            StartupConfig.CommandMode);
        Validate.Required(cfg.Api?.Url, "component.api.url", filePath);

        if (cfg.Startup?.IsInProcess == true)
            Validate.Required(cfg.Startup.Settings, "component.startup.settings", filePath);

        if (cfg.Startup?.IsCommand == true)
        {
            Validate.Required(cfg.Startup.Command, "component.startup.command", filePath);
            Validate.ExactlyOneSet("component.startup.readiness", filePath,
                ("url", cfg.Startup.Readiness?.Url),
                ("port", cfg.Startup.Readiness?.Port));
        }

        ValidateFilter(cfg.Filter, "component", filePath);
        cfg.Auth?.ValidateAuth("component", filePath);
    }

    private static void ValidateIntegrationEnv(IntegrationConfig cfg, string env, string filePath)
    {
        Validate.Required(cfg.Api?.Url, $"integration.{env}.api.url", filePath);
        ValidateFilter(cfg.Filter, $"integration.{env}", filePath);
        cfg.Auth?.ValidateAuth($"integration.{env}", filePath);
    }

    private static void ValidateFilter(FilterConfig? filter, string parent, string filePath)
    {
        if (filter is null) return;
        Validate.Required(filter.Strategy, $"{parent}.filter.strategy", filePath);
        Validate.OneOf(filter.Strategy!, $"{parent}.filter.strategy", filePath, "tags", "tests");
        Validate.Required(filter.EnvVariable, $"{parent}.filter.envVariable", filePath);
    }

    #endregion

    #region Parsing

    private static JObject ParseYaml(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Suite config file not found: {filePath}", filePath);
        try
        {
            return YamlConverter.ToJObject(File.ReadAllText(filePath));
        }
        catch (YamlException ex)
        {
            throw new InvalidDataException($"Invalid YAML in suite config {filePath}: {ex.Message}", ex);
        }
    }

    private static JObject RequireSection(JObject root, string key, string filePath) =>
        root[key] as JObject ?? throw ConfigError(filePath, $"Missing required '{key}' section");

    private static void ResolveEnvVars(JObject obj, string path, string filePath)
    {
        foreach (var prop in obj.Properties().ToList())
            switch (prop.Value)
            {
                case JValue { Type: JTokenType.String } jv:
                    prop.Value = new JValue(ExpandEnvVar(jv.Value<string>()!, $"{path}.{prop.Name}", filePath));
                    break;
                case JObject nested:
                    ResolveEnvVars(nested, $"{path}.{prop.Name}", filePath);
                    break;
            }
    }

    private static string ExpandEnvVar(string value, string fieldPath, string filePath) =>
        Regex.Replace(value, @"\$\{([A-Z_][A-Z0-9_]*)\}", match =>
        {
            var name = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(name)
                   ?? throw ConfigError(filePath,
                       $"Unresolved env var '${{{name}}}' in '{fieldPath}'. Set {name} before running tests.");
        });

    #endregion

    #region Serialization

    private static T Deserialize<T>(JObject obj) =>
        JsonConvert.DeserializeObject<T>(obj.ToString(), new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        })!;

    private static InvalidDataException ConfigError(string filePath, string message) =>
        new($"{message} [{filePath}]");

    #endregion
}

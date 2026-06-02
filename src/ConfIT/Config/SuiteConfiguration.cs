using System.IO;
using System.Text.RegularExpressions;
using ConfIT.Server.Launcher;
using ConfIT.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;

namespace ConfIT.Config;

// ── Public API ─────────────────────────────────────────────────────────────
public static class SuiteConfiguration
{
    private static readonly IReadOnlySet<string> RootKeys        = KeySet("component", "integration");
    private static readonly IReadOnlySet<string> ComponentKeys   = KeySet("startup", "api", "mock", "folders", "filter");
    private static readonly IReadOnlySet<string> StartupKeys     = KeySet("mode", "settings", "command", "readiness", "env");
    private static readonly IReadOnlySet<string> ReadinessKeys   = KeySet("url", "port", "timeoutSeconds", "intervalMs");
    private static readonly IReadOnlySet<string> ApiKeys         = KeySet("url", "authToken");
    private static readonly IReadOnlySet<string> MockKeys        = KeySet("url");
    private static readonly IReadOnlySet<string> FolderKeys      = KeySet("response", "requestBody", "responseBody");
    private static readonly IReadOnlySet<string> FilterKeys      = KeySet("strategy", "envVariable");
    private static readonly IReadOnlySet<string> EnvironmentKeys = KeySet("api", "folders", "filter");

    public static ComponentConfig LoadComponent(string filePath)
    {
        var root = LoadAndParse(filePath);
        Validate.KnownKeys(root, RootKeys, "root", filePath);

        var component = root["component"] as JObject
            ?? throw new InvalidDataException($"No 'component' section found in {filePath}");
        Validate.KnownKeys(component, ComponentKeys, "component", filePath);

        var startup = component["startup"] as JObject
            ?? throw new InvalidDataException($"'component.startup' is required in {filePath}");
        Validate.KnownKeys(startup, StartupKeys, "component.startup", filePath);

        var mode = startup["mode"]?.Value<string>() ?? "in-process";
        Validate.OneOf(mode, "component.startup.mode", filePath, StartupConfig.InProcessMode, StartupConfig.CommandMode);

        if (mode == StartupConfig.InProcessMode)
        {
            Validate.Required(startup["settings"]?.Value<string>(), "component.startup.settings", filePath);
        }
        else
        {
            Validate.Required(startup["command"]?.Value<string>(), "component.startup.command", filePath);
            var readiness = startup["readiness"] as JObject
                ?? throw new InvalidDataException(
                    $"'component.startup.readiness' is required when mode is 'command' in {filePath}");
            Validate.KnownKeys(readiness, ReadinessKeys, "component.startup.readiness", filePath);
            Validate.ExactlyOneSet("component.startup.readiness", filePath,
                ("url",  readiness["url"]),
                ("port", readiness["port"]));
        }

        var api = component["api"] as JObject
            ?? throw new InvalidDataException($"'component.api' is required in {filePath}");
        Validate.KnownKeys(api, ApiKeys, "component.api", filePath);
        Validate.Required(api["url"]?.Value<string>(), "component.api.url", filePath);

        if (component["mock"]    is JObject mock)    Validate.KnownKeys(mock,    MockKeys,   "component.mock",    filePath);
        if (component["folders"] is JObject folders) Validate.KnownKeys(folders, FolderKeys, "component.folders", filePath);

        ValidateFilter(component["filter"] as JObject, "component", filePath);

        if (startup["mode"] is null) startup["mode"] = StartupConfig.InProcessMode;

        ResolveEnvVars(component, "component", filePath);
        return Deserialize<ComponentConfig>(component);
    }

    public static IntegrationEnvironmentConfig LoadIntegration(string filePath, string? environment = null)
    {
        var root = LoadAndParse(filePath);
        Validate.KnownKeys(root, RootKeys, "root", filePath);

        var integration = root["integration"] as JObject
            ?? throw new InvalidDataException($"No 'integration' section found in {filePath}");

        var activeEnv = environment
            ?? Environment.GetEnvironmentVariable("TEST_ENVIRONMENT")
            ?? integration["default"]?.Value<string>()
            ?? throw new InvalidDataException(
                $"Cannot determine active environment in {filePath}. " +
                "Set TEST_ENVIRONMENT, pass an environment argument, " +
                "or add 'default: <name>' to the integration section.");

        var envBlock = integration[activeEnv] as JObject
            ?? throw new InvalidDataException(
                $"No environment '{activeEnv}' found in the integration section of {filePath}");

        Validate.KnownKeys(envBlock, EnvironmentKeys, $"integration.{activeEnv}", filePath);

        var api = envBlock["api"] as JObject
            ?? throw new InvalidDataException(
                $"'integration.{activeEnv}.api' is required in {filePath}");
        Validate.KnownKeys(api, ApiKeys, $"integration.{activeEnv}.api", filePath);
        Validate.Required(api["url"]?.Value<string>(), $"integration.{activeEnv}.api.url", filePath);

        if (envBlock["folders"] is JObject folders)
            Validate.KnownKeys(folders, FolderKeys, $"integration.{activeEnv}.folders", filePath);

        ValidateFilter(envBlock["filter"] as JObject, $"integration.{activeEnv}", filePath);

        ResolveEnvVars(envBlock, $"integration.{activeEnv}", filePath);
        return Deserialize<IntegrationEnvironmentConfig>(envBlock);
    }

    private static void ValidateFilter(JObject? filter, string parent, string filePath)
    {
        if (filter is null) return;
        Validate.KnownKeys(filter, FilterKeys, $"{parent}.filter", filePath);
        var strategy = filter["strategy"]?.Value<string>();
        Validate.Required(strategy, $"{parent}.filter.strategy", filePath);
        Validate.OneOf(strategy!, $"{parent}.filter.strategy", filePath, "tags", "tests");
        Validate.Required(filter["envVariable"]?.Value<string>(), $"{parent}.filter.envVariable", filePath);
    }

    private static JObject LoadAndParse(string filePath)
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

    private static void ResolveEnvVars(JObject obj, string path, string filePath)
    {
        foreach (var prop in obj.Properties().ToList())
        {
            switch (prop.Value)
            {
                case JValue { Type: JTokenType.String } jv:
                    prop.Value = new JValue(ResolveEnvVarString(jv.Value<string>()!, $"{path}.{prop.Name}", filePath));
                    break;
                case JObject nested:
                    ResolveEnvVars(nested, $"{path}.{prop.Name}", filePath);
                    break;
            }
        }
    }

    private static string ResolveEnvVarString(string value, string fieldPath, string filePath) =>
        Regex.Replace(value, @"\$\{([A-Z_][A-Z0-9_]*)\}", match =>
        {
            var name = match.Groups[1].Value;
            return Environment.GetEnvironmentVariable(name)
                ?? throw new InvalidDataException(
                    $"Unresolved env var '${{{name}}}' in '{fieldPath}' in {filePath}. " +
                    $"Set {name} in the environment before running tests.");
        });

    private static T Deserialize<T>(JObject obj) =>
        JsonConvert.DeserializeObject<T>(obj.ToString(), new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        })!;

    private static HashSet<string> KeySet(params string[] keys) => new(keys);
}

// ── Public config DTOs ─────────────────────────────────────────────────────

public sealed class ComponentConfig
{
    public StartupConfig Startup { get; set; } = new();
    public ApiConfig Api { get; set; } = new();
    public MockConfig? Mock { get; set; }
    public FolderConfig? Folders { get; set; }
    public FilterConfig? Filter { get; set; }
}

public sealed class StartupConfig
{
    public const string InProcessMode = "in-process";
    public const string CommandMode   = "command";

    public string Mode { get; set; } = InProcessMode;
    public bool IsInProcess => Mode == InProcessMode;
    public bool IsCommand   => Mode == CommandMode;
    public string? Settings { get; set; }
    public string? Command { get; set; }
    public ReadinessConfig? Readiness { get; set; }
    public Dictionary<string, string>? Env { get; set; }
}

public sealed class IntegrationEnvironmentConfig
{
    public ApiConfig Api { get; set; } = new();
    public FolderConfig? Folders { get; set; }
    public FilterConfig? Filter { get; set; }
}

public sealed class ApiConfig
{
    public string? Url { get; set; }
    public string? AuthToken { get; set; }
}

public sealed class MockConfig
{
    public string? Url { get; set; }
}

public sealed class FolderConfig
{
    public string? Response { get; set; }
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
}

public sealed class FilterConfig
{
    public string? Strategy { get; set; }
    public string? EnvVariable { get; set; }
}

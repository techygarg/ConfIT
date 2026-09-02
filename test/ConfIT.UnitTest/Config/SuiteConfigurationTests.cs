using ConfIT.Config;
using ConfIT.Config.AuthProvider;
using ConfIT.Runner.Boot;
using static ConfIT.UnitTest.Config.ConfigTestHelper;

namespace ConfIT.UnitTest.Config;

// Serialise all three classes so env var mutations don't race across parallel test runs.
[CollectionDefinition("SuiteConfiguration", DisableParallelization = true)]
public class SuiteConfigurationCollection
{
}

[Collection("SuiteConfiguration")]
public class SuiteConfigurationLoadComponentTests
{
    [Fact]
    public void LoadComponent_InProcessMode_ReturnsValidConfig()
    {
        var path = Write("""
                         component:
                           startup:
                             mode: in-process
                             settings: appsettings.Tests.json
                           api:
                             url: http://localhost:5170
                           mock:
                             url: http://localhost:8888
                           filter:
                             strategy: tags
                             envVariable: TEST_TAGS
                         """);
        try
        {
            var cfg = SuiteConfiguration.LoadComponent(path);
            Assert.Equal("in-process", cfg.Startup.Mode);
            Assert.True(cfg.Startup.IsInProcess);
            Assert.Equal("appsettings.Tests.json", cfg.Startup.Settings);
            Assert.Equal("http://localhost:5170", cfg.Api.Url);
            Assert.Equal("http://localhost:8888", cfg.Mock?.Url);
            Assert.Equal("tags", cfg.Filter?.Strategy);
            Assert.Equal("TEST_TAGS", cfg.Filter?.EnvVariable);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadComponent_MockEnableLogsTrue_ParsesFlag()
    {
        var path = Write("""
                         component:
                           startup:
                             mode: in-process
                             settings: appsettings.Tests.json
                           api:
                             url: http://localhost:5170
                           mock:
                             url: http://localhost:8888
                             enableLogs: true
                         """);
        try
        {
            var cfg = SuiteConfiguration.LoadComponent(path);

            Assert.True(cfg.Mock?.EnableLogs);
            Assert.True(cfg.ToSuiteConfig().EnableMockServerLogs);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadComponent_MockWithoutEnableLogs_DefaultsToFalse()
    {
        var path = Write("""
                         component:
                           startup:
                             mode: in-process
                             settings: appsettings.Tests.json
                           api:
                             url: http://localhost:5170
                           mock:
                             url: http://localhost:8888
                         """);
        try
        {
            Assert.False(SuiteConfiguration.LoadComponent(path).Mock?.EnableLogs);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadComponent_CommandMode_ReturnsValidConfig()
    {
        var path = Write("""
                         component:
                           startup:
                             mode: command
                             command: dotnet run --project ../User.Api
                             readiness:
                               url: http://localhost:5170/health
                               timeoutSeconds: 30
                           api:
                             url: http://localhost:5170
                         """);
        try
        {
            var cfg = SuiteConfiguration.LoadComponent(path);
            Assert.True(cfg.Startup.IsCommand);
            Assert.Equal("dotnet run --project ../User.Api", cfg.Startup.Command);
            Assert.Equal("http://localhost:5170/health", cfg.Startup.Readiness?.Url);
            Assert.Equal(30, cfg.Startup.Readiness?.TimeoutSeconds);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadComponent_ModeAbsent_DefaultsToInProcess()
    {
        var path = Write("""
                         component:
                           startup:
                             settings: appsettings.Tests.json
                           api:
                             url: http://localhost:5170
                         """);
        try
        {
            var cfg = SuiteConfiguration.LoadComponent(path);
            Assert.True(cfg.Startup.IsInProcess);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadComponent_FileMissing_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(
            () => SuiteConfiguration.LoadComponent("/no/such/file.yaml"));
    }

    [Fact]
    public void LoadComponent_MissingApiUrl_Throws()
    {
        var path = Write("""
                         component:
                           startup:
                             settings: s.json
                           api: {}
                         """);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SuiteConfiguration.LoadComponent(path));
            Assert.Contains("component.api.url", ex.Message);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadComponent_InProcessMissingSettings_Throws()
    {
        var path = Write("""
                         component:
                           startup:
                             mode: in-process
                           api:
                             url: http://localhost:5170
                         """);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SuiteConfiguration.LoadComponent(path));
            Assert.Contains("component.startup.settings", ex.Message);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadComponent_CommandBothUrlAndPort_Throws()
    {
        var path = Write("""
                         component:
                           startup:
                             mode: command
                             command: dotnet run
                             readiness:
                               url: http://localhost:5170/health
                               port: 5170
                           api:
                             url: http://localhost:5170
                         """);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SuiteConfiguration.LoadComponent(path));
            Assert.Contains("readiness", ex.Message);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadComponent_EnvVarResolved()
    {
        Environment.SetEnvironmentVariable("CONFIT_TEST_API_URL", "http://resolved:9000");
        var path = Write("""
                         component:
                           startup:
                             settings: s.json
                           api:
                             url: ${CONFIT_TEST_API_URL}
                         """);
        try
        {
            var cfg = SuiteConfiguration.LoadComponent(path);
            Assert.Equal("http://resolved:9000", cfg.Api.Url);
        }
        finally
        {
            Cleanup(path);
            Environment.SetEnvironmentVariable("CONFIT_TEST_API_URL", null);
        }
    }

    [Fact]
    public void LoadComponent_UnresolvedEnvVar_Throws()
    {
        Environment.SetEnvironmentVariable("CONFIT_MISSING_VAR", null);
        var path = Write("""
                         component:
                           startup:
                             settings: s.json
                           api:
                             url: ${CONFIT_MISSING_VAR}
                         """);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SuiteConfiguration.LoadComponent(path));
            Assert.Contains("CONFIT_MISSING_VAR", ex.Message);
        }
        finally
        {
            Cleanup(path);
        }
    }
}

[Collection("SuiteConfiguration")]
public class SuiteConfigurationLoadIntegrationTests
{
    [Fact]
    public void LoadIntegration_ExplicitEnvironment_ReturnsCorrectBlock()
    {
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:5170
                           qa:
                             api:
                               url: http://qa.internal
                         """);
        try
        {
            var cfg = SuiteConfiguration.LoadIntegration(path, "qa");
            Assert.Equal("http://qa.internal", cfg.Api.Url);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadIntegration_DefaultEnv_UsedWhenNoEnvSet()
    {
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", null);
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:5170
                         """);
        try
        {
            var cfg = SuiteConfiguration.LoadIntegration(path);
            Assert.Equal("http://localhost:5170", cfg.Api.Url);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadIntegration_TestEnvironmentEnvVar_OverridesDefault()
    {
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", "qa");
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:5170
                           qa:
                             api:
                               url: http://qa.internal
                         """);
        try
        {
            var cfg = SuiteConfiguration.LoadIntegration(path);
            Assert.Equal("http://qa.internal", cfg.Api.Url);
        }
        finally
        {
            Cleanup(path);
            Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", null);
        }
    }

    [Fact]
    public void LoadIntegration_NoEnvAndNoDefault_Throws()
    {
        Environment.SetEnvironmentVariable("TEST_ENVIRONMENT", null);
        var path = Write("""
                         integration:
                           local:
                             api:
                               url: http://localhost:5170
                         """);
        try
        {
            Assert.Throws<InvalidDataException>(() => SuiteConfiguration.LoadIntegration(path));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadIntegration_UnknownEnvironment_Throws()
    {
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:5170
                         """);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(
                () => SuiteConfiguration.LoadIntegration(path, "staging"));
            Assert.Contains("staging", ex.Message);
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public void LoadIntegration_BearerAuthEnvVar_Resolved()
    {
        Environment.SetEnvironmentVariable("QA_API_TOKEN_TEST", "my-secret");
        var path = Write("""
                         integration:
                           default: qa
                           qa:
                             api:
                               url: http://qa.internal
                             auth:
                               type: bearer
                               token: ${QA_API_TOKEN_TEST}
                         """);
        try
        {
            var cfg = SuiteConfiguration.LoadIntegration(path);
            Assert.NotNull(cfg.Auth);
            Assert.Equal("bearer", cfg.Auth!.Type);
            Assert.Equal("my-secret", cfg.Auth.Token);
        }
        finally
        {
            Cleanup(path);
            Environment.SetEnvironmentVariable("QA_API_TOKEN_TEST", null);
        }
    }
}

[Collection("SuiteConfiguration")]
public class SuiteConfigurationExtensionTests
{
    [Fact]
    public void ToSuiteConfig_Component_MapsAllFields()
    {
        var cfg = new ComponentConfig
        {
            Api = new ApiConfig { Url = "http://api:5170" },
            Mock = new MockConfig { Url = "http://mock:8888" },
            Folders = new FolderConfig { Response = "resp", RequestBody = "req", ResponseBody = "expResp" }
        };
        var sc = cfg.ToSuiteConfig();
        Assert.Equal("http://api:5170", sc.ApiServerUrl);
        Assert.Equal("http://mock:8888", sc.MockServerUrl);
        Assert.Equal("resp", sc.ApiResponseFolder);
        Assert.Equal("req", sc.RequestBodyFolder);
        Assert.Equal("expResp", sc.ResponseBodyFolder);
        Assert.False(sc.EnableMockServerLogs);
    }

    [Fact]
    public void ToSuiteConfig_MockEnableLogsTrue_EnablesMockServerLogs()
    {
        var cfg = new ComponentConfig
        {
            Api = new ApiConfig { Url = "http://api:5170" },
            Mock = new MockConfig { Url = "http://mock:8888", EnableLogs = true }
        };

        Assert.True(cfg.ToSuiteConfig().EnableMockServerLogs);
    }

    [Fact]
    public void ToSuiteConfig_NoMockSection_EnableMockServerLogsIsFalse()
    {
        var cfg = new ComponentConfig { Api = new ApiConfig { Url = "http://api:5170" } };

        Assert.False(cfg.ToSuiteConfig().EnableMockServerLogs);
    }

    [Fact]
    public void ToTestFilter_TagsStrategy_ReturnsNonNull()
    {
        var cfg = new ComponentConfig
        {
            Api = new ApiConfig { Url = "http://localhost" },
            Filter = new FilterConfig { Strategy = "tags", EnvVariable = "TEST_TAGS" }
        };
        Assert.NotNull(cfg.ToTestFilter());
    }

    [Fact]
    public void ToTestFilter_NullFilter_ReturnsNull()
    {
        var cfg = new ComponentConfig { Api = new ApiConfig { Url = "http://localhost" } };
        Assert.Null(cfg.ToTestFilter());
    }

    [Fact]
    public void ToAppLauncherConfig_CommandMode_MapsCorrectly()
    {
        var readiness = new ReadinessConfig { Url = "http://localhost/health" };
        var cfg = new ComponentConfig
        {
            Api = new ApiConfig { Url = "http://localhost" },
            Startup = new StartupConfig
            {
                Mode = "command", Command = "dotnet run",
                Readiness = readiness,
                Env = new Dictionary<string, string> { ["KEY"] = "val" }
            }
        };
        var lc = cfg.ToAppLauncherConfig();
        Assert.Equal("dotnet run", lc.Command);
        Assert.Same(readiness, lc.Readiness);
        Assert.Equal("val", lc.Env["KEY"]);
    }

    [Fact]
    public void ToAppLauncherConfig_InProcessMode_ThrowsInvalidOperationException()
    {
        var cfg = new ComponentConfig
        {
            Api = new ApiConfig { Url = "http://localhost" },
            Startup = new StartupConfig { Mode = "in-process" }
        };
        Assert.Throws<InvalidOperationException>(() => cfg.ToAppLauncherConfig());
    }
}

[Collection("SuiteConfiguration")]
public class SuiteConfigurationAuthValidationTests
{
    [Fact]
    public void LoadComponent_BearerAuthMissingToken_Throws()
    {
        var path = Write("""
                         component:
                           startup:
                             settings: s.json
                           api:
                             url: http://localhost
                           auth:
                             type: bearer
                         """);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SuiteConfiguration.LoadComponent(path));
            Assert.Contains("component.auth.token", ex.Message);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void LoadComponent_OAuth2MissingClientSecret_Throws()
    {
        var path = Write("""
                         component:
                           startup:
                             settings: s.json
                           api:
                             url: http://localhost
                           auth:
                             type: oauth2-client-credentials
                             tokenUrl: https://auth/token
                             clientId: my-id
                         """);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SuiteConfiguration.LoadComponent(path));
            Assert.Contains("component.auth.clientSecret", ex.Message);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void LoadComponent_ApiKeyMissingHeaderKey_Throws()
    {
        var path = Write("""
                         component:
                           startup:
                             settings: s.json
                           api:
                             url: http://localhost
                           auth:
                             type: api-key
                             value: my-key
                         """);
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => SuiteConfiguration.LoadComponent(path));
            Assert.Contains("component.auth.headerKey", ex.Message);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void LoadComponent_UnknownAuthType_Throws()
    {
        var path = Write("""
                         component:
                           startup:
                             settings: s.json
                           api:
                             url: http://localhost
                           auth:
                             type: digest
                         """);
        try
        {
            Assert.Throws<InvalidDataException>(() => SuiteConfiguration.LoadComponent(path));
        }
        finally { Cleanup(path); }
    }
}

[Collection("SuiteConfiguration")]
public class SuiteConfigurationAuthExtensionTests
{
    [Fact]
    public void ToAuthTokenProvider_NoAuthBlock_ReturnsNull()
    {
        var cfg = new ComponentConfig { Api = new ApiConfig { Url = "http://localhost" } };
        Assert.Null(cfg.ToAuthTokenProvider());
    }

    [Fact]
    public void ToAuthTokenProvider_Bearer_ReturnsBearerProvider()
    {
        var cfg = new ComponentConfig
        {
            Api  = new ApiConfig { Url = "http://localhost" },
            Auth = new AuthConfig { Type = "bearer", Token = "my-token" }
        };
        var provider = cfg.ToAuthTokenProvider();
        Assert.NotNull(provider);
        Assert.Equal("Authorization", provider!.HeaderKey());
        Assert.Equal("Bearer my-token", provider.Token());
    }

    [Fact]
    public void ToAuthTokenProvider_BearerWithCustomHeaderKey_UsesCustomKey()
    {
        var cfg = new ComponentConfig
        {
            Api  = new ApiConfig { Url = "http://localhost" },
            Auth = new AuthConfig { Type = "bearer", Token = "tok", HeaderKey = "X-Auth" }
        };
        var provider = cfg.ToAuthTokenProvider();
        Assert.Equal("X-Auth", provider!.HeaderKey());
    }

    [Fact]
    public void ToAuthTokenProvider_ApiKey_ReturnsApiKeyProvider()
    {
        var cfg = new ComponentConfig
        {
            Api  = new ApiConfig { Url = "http://localhost" },
            Auth = new AuthConfig { Type = "api-key", HeaderKey = "X-API-Key", Value = "secret" }
        };
        var provider = cfg.ToAuthTokenProvider();
        Assert.NotNull(provider);
        Assert.Equal("X-API-Key", provider!.HeaderKey());
        Assert.Equal("secret", provider.Token());
    }

    [Fact]
    public void ToAuthTokenProvider_IntegrationConfig_NoAuthBlock_ReturnsNull()
    {
        var cfg = new IntegrationConfig { Api = new ApiConfig { Url = "http://localhost" } };
        Assert.Null(cfg.ToAuthTokenProvider());
    }

    [Fact]
    public void ToAuthTokenProvider_IntegrationConfig_Bearer_ReturnsBearerProvider()
    {
        var cfg = new IntegrationConfig
        {
            Api  = new ApiConfig { Url = "http://localhost" },
            Auth = new AuthConfig { Type = "bearer", Token = "integration-token" }
        };
        var provider = cfg.ToAuthTokenProvider();
        Assert.Equal("Bearer integration-token", provider!.Token());
    }
}

internal static class ConfigTestHelper
{
    internal static string Write(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    internal static void Cleanup(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            /* best-effort */
        }
    }
}
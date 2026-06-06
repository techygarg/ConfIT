using ConfIT.Config;
using static ConfIT.UnitTest.Config.ConfigTestHelper;

namespace ConfIT.UnitTest;

public class SuiteBootstrapperTests
{
    #region ForIntegration — success paths

    [Fact]
    public void ForIntegration_MinimalConfig_BuildsContext()
    {
        // Given
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:9999
                         """);
        try
        {
            // When
            using var suite = SuiteBootstrapper.ForIntegration(path);

            // Then
            suite.Context.Should().NotBeNull();
            suite.Context.HttpClient.Should().NotBeNull();
            suite.Context.Config.ApiServerUrl.Should().Be("http://localhost:9999");
            suite.Context.Config.CustomMatchers.Should().BeEmpty();
            suite.Context.Filter.Should().BeNull();
            suite.Context.ResultCollector.Should().NotBeNull();
            suite.Services.Should().BeNull();
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ForIntegration_WithFilter_BuildsTestFilter()
    {
        // Given
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:9999
                             filter:
                               strategy: tags
                               envVariable: RUN_POOLS
                         """);
        try
        {
            // When
            using var suite = SuiteBootstrapper.ForIntegration(path);

            // Then — filter is built; env var RUN_POOLS is not set so Tags is empty
            suite.Context.Filter.Should().NotBeNull();
            suite.Context.Filter!.Tags.Should().BeEmpty();
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ForIntegration_WithFolders_EnsuresResponseDirectory()
    {
        // Given
        var tempDir      = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var responseDir  = Path.Combine(tempDir, "responses");
        Directory.CreateDirectory(tempDir);

        var path = Write($"""
                          integration:
                            default: local
                            local:
                              api:
                                url: http://localhost:9999
                              folders:
                                response: {responseDir}
                          """);
        try
        {
            // When
            using var suite = SuiteBootstrapper.ForIntegration(path);

            // Then — response folder created and path stored as absolute
            Directory.Exists(responseDir).Should().BeTrue();
            suite.Context.Config.ApiResponseFolder.Should().Be(
                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, responseDir)));
        }
        finally
        {
            Cleanup(path);
            try { Directory.Delete(tempDir, true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ForIntegration_WithCustomMatchers_RegistersMatchers()
    {
        // Given
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:9999
                         """);
        var custom = new Dictionary<string, SemanticMatcherFunc>
        {
            ["isPositive"] = (v, _) => v.Value<int>() > 0 ? null : "expected positive"
        };

        try
        {
            // When
            using var suite = SuiteBootstrapper.ForIntegration(path, customMatchers: custom);

            // Then
            suite.Context.Config.CustomMatchers.Should().ContainKey("isPositive");
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ForIntegration_WithExplicitEnvironment_UsesNamedEnvironment()
    {
        // Given
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:9999
                           staging:
                             api:
                               url: http://staging.example.com
                         """);
        try
        {
            // When
            using var suite = SuiteBootstrapper.ForIntegration(path, environment: "staging");

            // Then
            suite.Context.Config.ApiServerUrl.Should().Be("http://staging.example.com");
        }
        finally { Cleanup(path); }
    }

    #endregion

    #region ForIntegration — disposal

    [Fact]
    public void ForIntegration_Dispose_PrintsSummaryWithoutThrowing()
    {
        // Given
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:9999
                         """);
        try
        {
            var suite = SuiteBootstrapper.ForIntegration(path);

            // When / Then — Dispose prints summary and cleans up; must not throw
            var act = () => suite.Dispose();
            act.Should().NotThrow();
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ForIntegration_DoubleDispose_IsIdempotent()
    {
        // Given
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:9999
                         """);
        try
        {
            var suite = SuiteBootstrapper.ForIntegration(path);
            suite.Dispose();

            // When / Then — second Dispose must not throw
            var act = () => suite.Dispose();
            act.Should().NotThrow();
        }
        finally { Cleanup(path); }
    }

    #endregion

    #region ForIntegration — error cases

    [Fact]
    public void ForIntegration_MissingFile_ThrowsFileNotFoundException()
    {
        var act = () => SuiteBootstrapper.ForIntegration("does-not-exist.yaml");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ForIntegration_MissingApiUrl_ThrowsInvalidDataException()
    {
        var path = Write("""
                         integration:
                           default: local
                           local: {}
                         """);
        try
        {
            var act = () => SuiteBootstrapper.ForIntegration(path);
            act.Should().Throw<InvalidDataException>().WithMessage("*api.url*required*");
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void ForIntegration_UnknownEnvironment_ThrowsInvalidDataException()
    {
        var path = Write("""
                         integration:
                           default: local
                           local:
                             api:
                               url: http://localhost:9999
                         """);
        try
        {
            var act = () => SuiteBootstrapper.ForIntegration(path, environment: "nonexistent");
            act.Should().Throw<InvalidDataException>().WithMessage("*nonexistent*");
        }
        finally { Cleanup(path); }
    }

    #endregion

    #region ForCommand — error cases (no external process needed)

    [Fact]
    public void ForCommand_WhenConfigIsInProcessMode_Throws()
    {
        // ForCommand requires startup.mode: command — calling it with in-process YAML is a caller error
        var path = Write("""
                         component:
                           startup:
                             mode: in-process
                             settings: appsettings.json
                           api:
                             url: http://localhost:9999
                         """);
        try
        {
            var act = () => SuiteBootstrapper.ForCommand(path);
            act.Should().Throw<InvalidOperationException>().WithMessage("*command*");
        }
        finally { Cleanup(path); }
    }

    #endregion
}

using YamlDotNet.Core;

namespace ConfIT.UnitTest.Util;

public class YamlConverterTests
{
    // ── Type mapping ───────────────────────────────────────────────────────────

    [Fact]
    public void ToJObject_BasicScalars_MapToCorrectJsonTypes()
    {
        // Given
        var yaml = """
            strVal:  hello
            intVal:  42
            boolVal: true
            nullVal: ~
            """;

        // When
        var result = YamlConverter.ToJObject(yaml);

        // Then
        result["strVal"]!.Type.Should().Be(JTokenType.String);
        result["intVal"]!.Type.Should().Be(JTokenType.Integer);
        result["boolVal"]!.Type.Should().Be(JTokenType.Boolean);
        result["nullVal"]!.Type.Should().Be(JTokenType.Null);
    }

    [Fact]
    public void ToJObject_NestedStructure_PreservesHierarchy()
    {
        // Given
        var yaml = """
            api:
              request:
                method: POST
                path: /api/user
              response:
                statusCode: 201
            """;

        // When
        var result = YamlConverter.ToJObject(yaml);

        // Then
        result["api"]!["request"]!["method"]!.Value<string>().Should().Be("POST");
        result["api"]!["request"]!["path"]!.Value<string>().Should().Be("/api/user");
        result["api"]!["response"]!["statusCode"]!.Value<int>().Should().Be(201);
    }

    [Fact]
    public void ToJObject_ArrayValues_PreserveElements()
    {
        // Given
        var yaml = """
            tags:
              - user
              - smoke
            """;

        // When
        var result = YamlConverter.ToJObject(yaml);

        // Then
        result["tags"]!.Should().HaveCount(2);
        result["tags"]![0]!.Value<string>().Should().Be("user");
        result["tags"]![1]!.Value<string>().Should().Be("smoke");
    }

    // ── YAML-specific features ─────────────────────────────────────────────────

    [Fact]
    public void ToJObject_YamlAnchorsAndAliases_ExpandToSameContentAsInline()
    {
        // Given — anchor defined once, aliased by two distinct nodes.
        // Uses direct alias (*defaults replaces the node) rather than YAML 1.1
        // merge keys (<<: *defaults), which are not supported in dynamic deserialization.
        var yaml = """
            defaults: &defaults
              method: GET
              path: /api/test
            TestA:
              request: *defaults
            TestB:
              request: *defaults
            """;

        // When
        var result = YamlConverter.ToJObject(yaml);

        // Then — both nodes have the anchor content fully expanded
        result["TestA"]!["request"]!["method"]!.Value<string>().Should().Be("GET");
        result["TestA"]!["request"]!["path"]!.Value<string>().Should().Be("/api/test");
        result["TestB"]!["request"]!["method"]!.Value<string>().Should().Be("GET");
        result["TestB"]!["request"]!["path"]!.Value<string>().Should().Be("/api/test");
    }

    [Fact]
    public void ToJObject_YamlComments_AreStrippedTransparently()
    {
        // Given
        var yaml = """
            # standalone comment
            name: alice # inline comment
            age: 30
            """;

        // When
        var result = YamlConverter.ToJObject(yaml);

        // Then — only the two data properties survive; no comment artefacts
        result.Properties().Should().HaveCount(2);
        result["name"]!.Value<string>().Should().Be("alice");
        result["age"]!.Value<int>().Should().Be(30);
    }

    // ── Error handling ─────────────────────────────────────────────────────────

    [Fact]
    public void ToJObject_InvalidYaml_ThrowsYamlException()
    {
        // Given — tab character is forbidden for indentation in YAML
        var invalidYaml = "root:\n\tchild: value";

        // When
        var act = () => YamlConverter.ToJObject(invalidYaml);

        // Then
        act.Should().Throw<YamlException>();
    }
}

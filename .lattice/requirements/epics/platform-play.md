---
epic: Platform Play
status: planned
---

# Platform Play

Features that expand ConfIT's reach beyond the .NET/xUnit ecosystem.

## Features

<!-- GENERATED — regenerated from features/*.md frontmatter where epic matches, do not hand-edit below -->

| Feature | Summary |
|---|---|
| [TOOL-003] CLI Tool | `dotnet tool install -g confit` — run tests without xUnit or C# glue code. |
| [TOOL-004] VS Code Extension + JSON Schema | Autocomplete and validation for test files; publish schema to SchemaStore.org. |
| [REACH-001] NUnit / MSTest Adapters | Thin adapters so ConfIT works with NUnit and MSTest, not just xUnit. |
| [[INT-002] GraphQL Support](../features/int-002-graphql-support.md) | First-class GraphQL query/mutation test definitions with the same matcher model. |
| [MOCK-002] Chaos / Fault Injection | Add delays, connection drops, partial responses to mock interactions for resilience testing. |
| [TOOL-005] Custom Matcher Plugin API | Register project-specific matchers (domain IDs, enum values) without forking the library. |

<!-- END GENERATED -->

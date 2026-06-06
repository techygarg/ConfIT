# Matchers and Patterns

API responses often contain fields that are dynamic — generated IDs, timestamps, server-assigned values — that can't be matched with an exact expected value. ConfIT's matcher system lets you express the right assertion for each field without writing C# code.

All matchers are declared inside the `matcher` key of the `response` section.

```json
"response": {
  "statusCode": 200,
  "body": { ... },
  "matcher": {
    "ignore":   [...],
    "pattern":  { ... },
    "semantic": { ... }
  }
}
```

The three matcher types are independent and can be combined in the same test. They are applied in this order:
1. `semantic` — validate and remove named-matcher fields
2. `pattern` — validate and remove regex-matched fields
3. `ignore` — remove excluded fields
4. Structural diff on whatever remains

---

## `ignore`

Removes the listed fields from both the actual and expected response before the structural diff. Use when a field is dynamic and you have no assertion to make about its value — only that it exists.

**Syntax:** array of field paths.

```json
"matcher": {
  "ignore": ["id", "createdAt"]
}
```

**Example — ignore a server-generated ID:**

```json
"response": {
  "statusCode": 200,
  "body": {
    "name": "test",
    "email": "test@test.com",
    "age": 10
  },
  "matcher": {
    "ignore": ["id"]
  }
}
```

The `id` field is present in the actual response but excluded from comparison. The remaining fields are diffed exactly.

📄 Live example: [`User.IntegrationTests/TestCase/user.json` — `ShouldReturnUserForGivenId_V1`](../example/User.IntegrationTests/TestCase/user.json)

---

## `pattern`

Validates a field's value against a regular expression, then removes it from both sides before the structural diff. Use when you know the format or range of a value but not its exact content.

**Syntax:** object mapping field path → regex string.

```json
"matcher": {
  "pattern": {
    "id": "^[0-9a-f-]{36}$"
  }
}
```

The field is removed from both actual and expected only if the regex matches. If it doesn't match, the field remains and the diff will fail — surfacing the mismatch.

**Example — assert ID is within a numeric range:**

```json
"response": {
  "statusCode": 201,
  "body": {},
  "matcher": {
    "pattern": {
      "id": "^(0|[1-9][0-9]?|100)$"
    }
  }
}
```

📄 Live example: [`User.IntegrationTests/TestCase/user.json` — `ShouldCreateAUser`](../example/User.IntegrationTests/TestCase/user.json)

**Example — multiple pattern fields:**

```json
"matcher": {
  "pattern": {
    "id":  "^(0|[1-9][0-9]?|100)$",
    "age": "^(0|[1-9][0-9]?|100)$"
  }
}
```

📄 Live example: [`User.IntegrationTests/TestCase/user.json` — `ShouldReturnUserForGivenId_V2`](../example/User.IntegrationTests/TestCase/user.json)

---

## `semantic`

Named, type-aware matchers. Instead of writing regex, declare what a field *is* using a built-in matcher name. Validated fields are removed from both sides before the structural diff.

**Semantic matchers are the enhanced version of `pattern`.** The same mechanic — validate then remove — but without the regex. Where `pattern` requires `^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-...$`, semantic lets you write `"isUuid"`. The intent is immediately readable, there's nothing to get wrong, and failure messages name the field and matcher rather than showing a diff.

**Decision guide — which matcher to use:**
- **Use `semantic`** when a built-in covers your case. Prefer it over `pattern` for any standard format, type, or range check.
- **Use `pattern`** when you need a specific regex that no built-in expresses — a custom format, a constrained string shape, a legacy code pattern.
- **Register a custom matcher** (via `SuiteConfig.CustomMatchers`) when the same domain-specific assertion repeats across multiple tests and deserves a name of its own.
- **Use `ignore`** only when you have no assertion to make about the field at all — not its format, not its type, not its range.

**Syntax:** object mapping field path → matcher spec (name or `name(param)`).

```json
"matcher": {
  "semantic": {
    "id":        "isUuid",
    "createdAt": "isIsoDateTime",
    "count":     "greaterThan(0)",
    "name":      "hasLength(1,100)"
  }
}
```

If a field listed under `semantic` is absent from the actual response, the test fails explicitly — it does not silently pass.

### Format matchers

| Matcher | Validates |
|---|---|
| `isUuid` | RFC 4122 UUID — `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` (case-insensitive) |
| `isIsoDate` | Date only — `yyyy-MM-dd` |
| `isIsoDateTime` | ISO 8601 datetime — with or without timezone (`Z` or `+HH:mm`) |
| `isEmail` | Email address format — `local@domain.tld` |

```json
"matcher": {
  "semantic": {
    "id":        "isUuid",
    "dob":       "isIsoDate",
    "createdAt": "isIsoDateTime",
    "email":     "isEmail"
  }
}
```

### Type matchers

| Matcher | Validates |
|---|---|
| `isNull` | Field value is JSON `null` |
| `isNotNull` | Field value is not `null` |

```json
"matcher": {
  "semantic": {
    "deletedAt": "isNull",
    "id":        "isNotNull"
  }
}
```

### Emptiness matchers

| Matcher | Validates |
|---|---|
| `isEmpty` | String is `""`, array has 0 elements, or object has 0 properties |
| `isNotEmpty` | Inverse of `isEmpty` |

```json
"matcher": {
  "semantic": {
    "errors": "isEmpty",
    "items":  "isNotEmpty"
  }
}
```

> `isEmpty` / `isNotEmpty` do not accept `null` — use `isNull` for null checks.

### Numeric matchers

| Matcher | Validates |
|---|---|
| `greaterThan(n)` | Numeric field is strictly greater than `n` |
| `lessThan(n)` | Numeric field is strictly less than `n` |

The field must be a JSON number. Applying a numeric matcher to a string fails with a type-mismatch message.

```json
"matcher": {
  "semantic": {
    "id":    "greaterThan(0)",
    "age":   "lessThan(150)",
    "score": "greaterThan(0.5)"
  }
}
```

### Size matchers

| Matcher | Validates |
|---|---|
| `hasLength(n)` | String length, array element count, or object property count equals `n` |
| `hasLength(min,max)` | Length is within `[min, max]` — both bounds inclusive |

```json
"matcher": {
  "semantic": {
    "zip":   "hasLength(5)",
    "name":  "hasLength(1,100)",
    "tags":  "hasLength(3)"
  }
}
```

**Example — combining format, numeric, and size matchers in one test:**

```json
"response": {
  "statusCode": 200,
  "body": {
    "name": "test"
  },
  "matcher": {
    "semantic": {
      "id":    "greaterThan(0)",
      "email": "isEmail",
      "age":   "greaterThan(0)",
      "name":  "hasLength(1,100)"
    }
  }
}
```

📄 Live example (integration): [`User.IntegrationTests/TestCase/semanticMatchers.json`](../example/User.IntegrationTests/TestCase/semanticMatchers.json)  
📄 Live example (component): [`User.ComponentTests/TestCase/user.json` — `ShouldValidateUserFieldsWithSemanticMatchers`](../example/User.ComponentTests/TestCase/user.json)

---

## Nested field paths

All three matcher types support targeting fields at any depth using `__` as the path separator.

```
parent__child__field  →  parent.child.field
```

**Example — `ignore` a deeply nested field:**

```json
"matcher": {
  "ignore": ["child3__child4__pincode", "child5"]
}
```

📄 Live example: [`User.IntegrationTests/TestCase/multiLevel.json` — `ShouldIgnoreFieldInMultiLevelParent`](../example/User.IntegrationTests/TestCase/multiLevel.json)

**Example — `pattern` on a nested field:**

```json
"matcher": {
  "pattern": {
    "child3__child4__pincode": "^[0-9]{1,6}$"
  }
}
```

📄 Live example: [`User.IntegrationTests/TestCase/multiLevel.json` — `ShouldApplyMatcherInMultiLevelParent`](../example/User.IntegrationTests/TestCase/multiLevel.json)

**Example — `semantic` on a nested field:**

```json
"matcher": {
  "semantic": {
    "user__profile__id": "isUuid"
  }
}
```

When a `__` path does not resolve to a field in the actual response, the test fails explicitly.

---

## Combining matcher types

All three types can be used together in the same test. Each type handles a different set of fields — they do not interfere.

```json
"matcher": {
  "ignore":   ["createdAt"],
  "pattern":  { "legacyCode": "^[A-Z]{3}-\\d+$" },
  "semantic": { "id": "isUuid", "count": "greaterThan(0)" }
}
```

Processing order: `semantic` fields are validated and removed first, then `pattern`, then `ignore`. The structural diff only sees fields not claimed by any matcher.

---

## Custom matchers

For domain-specific assertions (e.g., proprietary ID formats, enum values), register additional matchers in your fixture. Custom matchers follow the same `name` or `name(param)` syntax in the DSL.

**`SemanticMatcherFunc` signature:**

```csharp
// Return null on success; return a failure message string on failure
public delegate string? SemanticMatcherFunc(JToken value, string? parameter);
```

**Registration in fixture setup (bootstrapped path):**

```csharp
_suite = SuiteBootstrapper.ForIntegration("suite.config.yaml",
    customMatchers: new Dictionary<string, SemanticMatcherFunc>
    {
        ["isDomainId"] = (token, _) =>
            token.Value<string>()?.StartsWith("DOM-") == true
                ? null
                : "Expected domain ID format DOM-{n}"
    }
};
```

**Usage in DSL:**

```json
"matcher": {
  "semantic": {
    "domainRef": "isDomainId"
  }
}
```

**Constraints:**
- Custom matcher names must not conflict with built-in names — an attempt throws `ArgumentException` before any HTTP call.
- Custom matchers are scoped to the suite where they are registered.

---

## Quick reference

| Matcher | Type | Example |
|---|---|---|
| `ignore` | Remove field from diff | `"ignore": ["id", "createdAt"]` |
| `pattern` | Regex validation | `"pattern": { "id": "^[0-9]+$" }` |
| `isUuid` | UUID format | `"id": "isUuid"` |
| `isIsoDate` | Date only | `"dob": "isIsoDate"` |
| `isIsoDateTime` | Datetime with/without tz | `"createdAt": "isIsoDateTime"` |
| `isEmail` | Email format | `"email": "isEmail"` |
| `isNull` | JSON null | `"deletedAt": "isNull"` |
| `isNotNull` | Not null | `"id": "isNotNull"` |
| `isEmpty` | Empty string / array / object | `"errors": "isEmpty"` |
| `isNotEmpty` | Non-empty string / array / object | `"items": "isNotEmpty"` |
| `greaterThan(n)` | Numeric > n | `"count": "greaterThan(0)"` |
| `lessThan(n)` | Numeric < n | `"age": "lessThan(150)"` |
| `hasLength(n)` | Exact length | `"zip": "hasLength(5)"` |
| `hasLength(min,max)` | Length within range (inclusive) | `"name": "hasLength(1,100)"` |
| Custom | Domain-specific | `"ref": "isDomainId"` |

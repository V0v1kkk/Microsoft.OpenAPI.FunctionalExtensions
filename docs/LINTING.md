# OpenAPI Linting

The `Microsoft.OpenAPI.FunctionalExtensions.Linting` library validates OpenAPI documents against a set of built-in documentation, structure, and example rules. Rules return `LintViolation` values with a severity, message, and location. The orchestrator aggregates violations into a `LintResult`.

## Built-in rules

| Rule ID | Severity | Description |
|---------|----------|-------------|
| `missing-operation-id` | Error | Operations must define a non-empty `operationId`. |
| `empty-operation-summary` | Warning | Operations should have a non-empty `summary`. |
| `empty-parameter-description` | Warning | Path and operation parameters should have a description. |
| `empty-schema-property-description` | Warning | Component schema properties should have a description. |
| `unused-schemas` | Warning | Component schemas not referenced from any path or operation. |
| `missing-response-description` | Error | Operation responses must have a description. |
| `path-without-operations` | Warning | Path items with no HTTP operations defined. |
| `missing-content-type` | Error | Responses that declare `content` but define no media types. |
| `invalid-examples` | Error / Info | Examples that do not match their schemas (type, enum, required properties, formats). |

## Quick validation

Run all default rules with a single call:

```fsharp
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter

let result = lintWithDefaults document

for violation in result.Violations do
    printfn "%s: %s" violation.Rule violation.Message
```

## Configuring rules

Use `LinterConfig` to control which rules run and how severities are reported.

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter
open Microsoft.OpenAPI.FunctionalExtensions.Linting.LinterConfig
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

// Disable specific default rules
let config =
    LinterConfig.defaults
    |> LinterConfig.without [ "empty-parameter-description"; "unused-schemas" ]

// Run only a subset of rules
let strictConfig =
    LinterConfig.defaults
    |> LinterConfig.withOnly [ "missing-operation-id"; "missing-response-description" ]

// Override severity for a rule
let relaxedConfig =
    LinterConfig.defaults
    |> LinterConfig.withSeverity "empty-operation-summary" Info

let result = lintWithConfig config document
```

### Custom rules

Append your own rules with `withCustom`. Custom rules use the same `LintRule` signature as built-in rules. Set the `Rule` field on each violation to a stable ID so severity overrides and CLI output can reference it.

```fsharp
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter
open Microsoft.OpenAPI.FunctionalExtensions.Linting.LinterConfig

let requireApiTitle (document: OpenApiDocument) : LintViolation list =
    match document.Info with
    | null -> []
    | info when String.IsNullOrWhiteSpace info.Title ->
        [ {
            Rule = "require-api-title"
            Severity = Error
            Message = "API title must be set in info.title."
            Location = DocumentLevel
          } ]
    | _ -> []

let config =
    LinterConfig.defaults
    |> LinterConfig.withCustom [ requireApiTitle ]

let result = lintWithConfig config document
```

## CLI `--lint` command

The `openapi-fx` tool includes a lint subcommand:

```bash
openapi-fx --lint --input Samples/petstore.yaml
openapi-fx --lint --input Samples/petstore.yaml --disable-rule empty-parameter-description
```

Output format (one line per violation):

```
[SEVERITY] location: message (rule-id)
```

Exit codes:

- `0` — no Error-level violations (warnings and info are allowed)
- `1` — one or more Error-level violations
- `2` — failed to read the input specification

## Module reference

| Module | Responsibility |
|--------|----------------|
| `Microsoft.OpenAPI.FunctionalExtensions.Linting.Types` | Domain types: `Severity`, `RuleId`, `NamedRule`, `LintLocation`, `LintViolation`, `LintResult`, `LintRule` |
| `Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules` | Built-in documentation and structure rule functions and `defaultNamedRules` |
| `Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation` | Example validation against schemas; `invalidExamples` rule |
| `Microsoft.OpenAPI.FunctionalExtensions.Linting.LinterConfig` | `LinterConfig` type and configuration helpers (`defaults`, `withOnly`, `without`, `withCustom`, `withSeverity`) |
| `Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter` | Orchestrator: `lintWithConfig`, `lintWithDefaults`, `defaultNamedRules` |

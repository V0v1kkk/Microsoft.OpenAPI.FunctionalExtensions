/// Orchestrates lint rule selection and execution against OpenAPI documents.
[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter

open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

/// All built-in default rules as named pairs, including example validation.
let defaultNamedRules: NamedRule list =
    Rules.defaultNamedRules @ [ ExampleValidation.invalidExamplesRule ]

let private isRuleEnabled (enabledRules: RuleId list option) (ruleId: RuleId) =
    match enabledRules with
    | None -> true
    | Some enabled -> List.contains ruleId enabled

let private isRuleDisabled (disabledRules: RuleId list) (ruleId: RuleId) =
    List.contains ruleId disabledRules

let private selectNamedRules (config: LinterConfig) : NamedRule list =
    defaultNamedRules
    |> List.filter (fun namedRule -> isRuleEnabled config.EnabledRules namedRule.Id)
    |> List.filter (fun namedRule -> not (isRuleDisabled config.DisabledRules namedRule.Id))

let private applySeverityOverrides (config: LinterConfig) (violations: LintViolation list) : LintViolation list =
    match config.Severity with
    | None -> violations
    | Some severityMap ->
        violations
        |> List.map (fun violation ->
            match Map.tryFind violation.Rule severityMap with
            | Some severity -> { violation with Severity = severity }
            | None -> violation)

/// Runs an explicit list of lint rules against a document.
let lint (rules: LintRule list) (document: OpenApiDocument) : LintResult = {
    Violations = rules |> List.collect (fun rule -> rule document)
    DocumentPath = None
}

/// Runs lint rules selected by <see cref="LinterConfig"/> against a document.
let lintWithConfig (config: LinterConfig) (document: OpenApiDocument) : LintResult =
    let rules =
        selectNamedRules config
        |> List.map (fun namedRule -> namedRule.Rule)
        |> fun selected -> selected @ config.CustomRules

    let violations =
        rules
        |> List.collect (fun rule -> rule document)
        |> applySeverityOverrides config

    { Violations = violations; DocumentPath = None }

/// Runs all default lint rules against a document.
let lintWithDefaults (document: OpenApiDocument) : LintResult =
    lintWithConfig Microsoft.OpenAPI.FunctionalExtensions.Linting.LinterConfig.defaults document

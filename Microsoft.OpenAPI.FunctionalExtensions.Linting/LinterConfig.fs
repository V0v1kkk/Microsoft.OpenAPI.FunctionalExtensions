namespace Microsoft.OpenAPI.FunctionalExtensions.Linting

open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

/// Controls which lint rules run and how their severities are reported.
type LinterConfig = {
    /// When <c>Some</c>, only the listed rule IDs run. <c>None</c> enables all default rules.
    EnabledRules: RuleId list option
    /// Rule IDs excluded from the selected default rules.
    DisabledRules: RuleId list
    /// Additional user-provided rules appended after the selected defaults.
    CustomRules: LintRule list
    /// Optional per-rule severity overrides keyed by rule ID.
    Severity: Map<RuleId, Severity> option
}

/// Helpers for building <see cref="LinterConfig"/> values.
[<RequireQualifiedAccess>]
module LinterConfig =

    /// All default rules enabled, with no custom rules or severity overrides.
    let defaults: LinterConfig = {
        EnabledRules = None
        DisabledRules = []
        CustomRules = []
        Severity = None
    }

    /// Restrict linting to only the given rule IDs.
    let withOnly (ruleIds: RuleId list) (config: LinterConfig) : LinterConfig =
        { config with EnabledRules = Some ruleIds }

    /// Exclude the given rule IDs from the selected defaults.
    let without (ruleIds: RuleId list) (config: LinterConfig) : LinterConfig =
        { config with DisabledRules = config.DisabledRules @ ruleIds }

    /// Append custom lint rules to the configuration.
    let withCustom (rules: LintRule list) (config: LinterConfig) : LinterConfig =
        { config with CustomRules = config.CustomRules @ rules }

    /// Override the reported severity for a specific rule ID.
    let withSeverity (ruleId: RuleId) (severity: Severity) (config: LinterConfig) : LinterConfig =
        let severityMap =
            match config.Severity with
            | None -> Map [ ruleId, severity ]
            | Some map -> Map.add ruleId severity map

        { config with Severity = Some severityMap }

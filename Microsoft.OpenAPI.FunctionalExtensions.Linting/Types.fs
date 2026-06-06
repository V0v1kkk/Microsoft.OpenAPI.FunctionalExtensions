/// Domain types for OpenAPI specification linting.
module Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

open Microsoft.OpenApi

/// Severity level assigned to a lint violation.
type Severity =
    | Error
    | Warning
    | Info

/// Stable identifier for a built-in or custom lint rule.
type RuleId = string

/// Where in the OpenAPI document a violation was found.
type LintLocation =
    | DocumentLevel
    | PathLevel of path: string
    | OperationLevel of path: string * method: string * operationId: string option
    | SchemaLevel of schemaName: string
    | SchemaPropertyLevel of schemaName: string * propertyName: string
    | ParameterLevel of path: string * method: string * parameterName: string

/// A single lint finding produced by a rule.
type LintViolation = {
    /// Rule identifier that produced this violation.
    Rule: RuleId
    Severity: Severity
    Message: string
    Location: LintLocation
}

/// Aggregated output from running one or more lint rules against a document.
type LintResult = {
    Violations: LintViolation list
    DocumentPath: string option
}

/// A lint rule inspects an OpenAPI document and returns zero or more violations.
type LintRule = OpenApiDocument -> LintViolation list

/// A lint rule paired with its stable identifier.
type NamedRule = {
    /// Unique rule identifier used in configuration and violation output.
    Id: RuleId
    /// Rule implementation that inspects a document and returns violations.
    Rule: LintRule
}

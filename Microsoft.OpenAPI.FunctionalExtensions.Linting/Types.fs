module Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

open Microsoft.OpenApi

type Severity =
    | Error
    | Warning
    | Info

type LintLocation =
    | DocumentLevel
    | PathLevel of path: string
    | OperationLevel of path: string * method: string * operationId: string option
    | SchemaLevel of schemaName: string
    | SchemaPropertyLevel of schemaName: string * propertyName: string
    | ParameterLevel of path: string * method: string * parameterName: string

type LintViolation = {
    Rule: string
    Severity: Severity
    Message: string
    Location: LintLocation
}

type LintResult = {
    Violations: LintViolation list
    DocumentPath: string option
}

type LintRule = OpenApiDocument -> LintViolation list

[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter

open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

let defaultRules: LintRule list = [
    Rules.missingOperationId
    Rules.emptyOperationSummary
    Rules.emptyParameterDescription
    Rules.emptySchemaPropertyDescription
    Rules.unusedSchemas
    Rules.missingResponseDescription
    Rules.pathWithoutOperations
    Rules.missingContentType
    ExampleValidation.invalidExamples
]

let lint (rules: LintRule list) (document: OpenApiDocument) : LintResult = {
    Violations = rules |> List.collect (fun rule -> rule document)
    DocumentPath = None
}

let lintWithDefaults (document: OpenApiDocument) : LintResult = lint defaultRules document

/// Built-in documentation and structure lint rules for OpenAPI documents.
[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules

open System
open System.Net.Http
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

let private isMissingText =
    function
    | None -> true
    | Some text -> String.IsNullOrWhiteSpace text

let private httpMethodName (method: HttpMethod) = method.ToString()

let private operationIdOption (operation: OpenApiOperation) = AdapterCore.ofObj operation.OperationId

let private parameterName (parameter: IOpenApiParameter) =
    AdapterCore.ofObj parameter.Name |> Option.defaultValue "<unnamed>"

let private parameterDescription (parameter: IOpenApiParameter) = AdapterCore.ofObj parameter.Description

let private responseDescription (response: IOpenApiResponse) = AdapterCore.ofObj response.Description

let private violation rule severity message location = {
    Rule = rule
    Severity = severity
    Message = message
    Location = location
}

let private operationContexts (document: OpenApiDocument) =
    DocumentAdapters.documentPaths document
    |> List.collect (fun (path, pathItem) ->
        let pathParameters = AdapterCore.readSeq pathItem.Parameters

        OperationAdapters.pathItemOperations pathItem
        |> List.map (fun (method, operation) -> path, method, operation, pathParameters))

let rec private addSchemaReferences
    (document: OpenApiDocument)
    (schema: IOpenApiSchema)
    (accumulated: Set<string>)
    : Set<string> =
    match schema with
    | null -> accumulated
    | unresolved ->
        match ReferenceAdapters.trySchemaReferenceId unresolved with
        | Some refName when Set.contains refName accumulated -> accumulated
        | Some refName ->
            let accumulatedWithRef = Set.add refName accumulated

            match DocumentAdapters.tryComponentSchema document refName with
            | None -> accumulatedWithRef
            | Some resolved -> addSchemaSubtreeReferences document resolved accumulatedWithRef
        | None -> addSchemaSubtreeReferences document unresolved accumulated

and private addSchemaSubtreeReferences
    (document: OpenApiDocument)
    (schema: IOpenApiSchema)
    (accumulated: Set<string>)
    : Set<string> =
    [
        yield! SchemaAdapters.schemaProperties schema |> Map.values |> Seq.toList
        yield!
            match SchemaAdapters.schemaItems schema with
            | Some items -> [ items ]
            | None -> []
        yield! SchemaAdapters.schemaAllOf schema
        yield! SchemaAdapters.schemaOneOf schema
        yield! SchemaAdapters.schemaAnyOf schema
        yield!
            match SchemaAdapters.schemaAdditionalProperties schema with
            | Some additionalProperties -> [ additionalProperties ]
            | None -> []
        yield!
            match SchemaAdapters.schemaNot schema with
            | Some notSchema -> [ notSchema ]
            | None -> []
    ]
    |> List.fold (fun state child -> addSchemaReferences document child state) accumulated

let private referencedSchemaNames (document: OpenApiDocument) : Set<string> =
    DocumentAdapters.allOperations document
    |> List.fold
        (fun accumulated (_, _, operation) ->
            OperationAdapters.schemasFromOperation operation
            |> List.fold (fun state schema -> addSchemaReferences document schema state) accumulated)
        Set.empty

/// Flags operations that do not define a non-empty <c>operationId</c>.
let missingOperationId (document: OpenApiDocument) : LintViolation list =
    operationContexts document
    |> List.choose (fun (path, method, operation, _) ->
        match operationIdOption operation with
        | Some operationId when not (String.IsNullOrWhiteSpace operationId) -> None
        | _ ->
            Some(
                violation
                    "missing-operation-id"
                    Error
                    "Operation must have a non-empty operationId."
                    (OperationLevel(path, httpMethodName method, operationIdOption operation))
            ))

/// Flags operations with a missing or blank <c>summary</c>.
let emptyOperationSummary (document: OpenApiDocument) : LintViolation list =
    operationContexts document
    |> List.choose (fun (path, method, operation, _) ->
        match AdapterCore.ofObj operation.Summary with
        | Some summary when not (String.IsNullOrWhiteSpace summary) -> None
        | _ ->
            Some(
                violation
                    "empty-operation-summary"
                    Warning
                    "Operation should have a non-empty summary."
                    (OperationLevel(path, httpMethodName method, operationIdOption operation))
            ))

/// Flags path and operation parameters without a description.
let emptyParameterDescription (document: OpenApiDocument) : LintViolation list =
    operationContexts document
    |> List.collect (fun (path, method, operation, pathParameters) ->
        let methodName = httpMethodName method

        (pathParameters @ OperationAdapters.operationParameters operation)
        |> List.choose (fun parameter ->
            match parameterDescription parameter with
            | Some description when not (String.IsNullOrWhiteSpace description) -> None
            | _ ->
                Some(
                    violation
                        "empty-parameter-description"
                        Warning
                        $"Parameter '{parameterName parameter}' should have a description."
                        (ParameterLevel(path, methodName, parameterName parameter))
                )))

/// Flags component schema properties without a description.
let emptySchemaPropertyDescription (document: OpenApiDocument) : LintViolation list =
    DocumentAdapters.documentSchemas document
    |> Map.toList
    |> List.collect (fun (schemaName, schema) ->
        SchemaAdapters.schemaProperties schema
        |> Map.toList
        |> List.choose (fun (propertyName, propertySchema) ->
            match SchemaAdapters.schemaDescription propertySchema with
            | Some description when not (String.IsNullOrWhiteSpace description) -> None
            | _ ->
                Some(
                    violation
                        "empty-schema-property-description"
                        Warning
                        $"Schema property '{propertyName}' should have a description."
                        (SchemaPropertyLevel(schemaName, propertyName))
                )))

/// Flags component schemas that are not referenced from any path or operation.
let unusedSchemas (document: OpenApiDocument) : LintViolation list =
    let referenced = referencedSchemaNames document

    DocumentAdapters.documentSchemas document
    |> Map.toList
    |> List.choose (fun (schemaName, _) ->
        if Set.contains schemaName referenced then
            None
        else
            Some(
                violation
                    "unused-schemas"
                    Warning
                    $"Component schema '{schemaName}' is not referenced from any path or operation."
                    (SchemaLevel schemaName)
            ))

/// Flags operation responses without a description.
let missingResponseDescription (document: OpenApiDocument) : LintViolation list =
    operationContexts document
    |> List.collect (fun (path, method, operation, _) ->
        let methodName = httpMethodName method
        let operationId = operationIdOption operation

        OperationAdapters.operationResponses operation
        |> Map.toList
        |> List.choose (fun (statusCode, response) ->
            match responseDescription response with
            | Some description when not (String.IsNullOrWhiteSpace description) -> None
            | _ ->
                Some(
                    violation
                        "missing-response-description"
                        Error
                        $"Response '{statusCode}' must have a description."
                        (OperationLevel(path, methodName, operationId))
                )))

/// Flags path items that define no HTTP operations.
let pathWithoutOperations (document: OpenApiDocument) : LintViolation list =
    DocumentAdapters.documentPaths document
    |> List.choose (fun (path, pathItem) ->
        match OperationAdapters.pathItemOperations pathItem with
        | [] ->
            Some(
                violation
                    "path-without-operations"
                    Warning
                    $"Path '{path}' has no operations defined."
                    (PathLevel path)
            )
        | _ -> None)

/// Flags responses that declare <c>content</c> but define no media types.
let missingContentType (document: OpenApiDocument) : LintViolation list =
    operationContexts document
    |> List.collect (fun (path, method, operation, _) ->
        let methodName = httpMethodName method
        let operationId = operationIdOption operation

        OperationAdapters.operationResponses operation
        |> Map.toList
        |> List.choose (fun (statusCode, response) ->
            match response with
            | null -> None
            | resp ->
                match AdapterCore.ofObj resp.Content with
                | None -> None
                | Some _ ->
                    let mediaTypes = AdapterCore.readMap resp.Content

                    if Map.isEmpty mediaTypes then
                        Some(
                            violation
                                "missing-content-type"
                                Error
                                $"Response '{statusCode}' has a content section but no media types."
                                (OperationLevel(path, methodName, operationId))
                        )
                    else
                        None))

/// Named rule: <c>missing-operation-id</c>.
let missingOperationIdRule: NamedRule = { Id = "missing-operation-id"; Rule = missingOperationId }

/// Named rule: <c>empty-operation-summary</c>.
let emptyOperationSummaryRule: NamedRule = { Id = "empty-operation-summary"; Rule = emptyOperationSummary }

/// Named rule: <c>empty-parameter-description</c>.
let emptyParameterDescriptionRule: NamedRule = { Id = "empty-parameter-description"; Rule = emptyParameterDescription }

/// Named rule: <c>empty-schema-property-description</c>.
let emptySchemaPropertyDescriptionRule: NamedRule =
    { Id = "empty-schema-property-description"; Rule = emptySchemaPropertyDescription }

/// Named rule: <c>unused-schemas</c>.
let unusedSchemasRule: NamedRule = { Id = "unused-schemas"; Rule = unusedSchemas }

/// Named rule: <c>missing-response-description</c>.
let missingResponseDescriptionRule: NamedRule = { Id = "missing-response-description"; Rule = missingResponseDescription }

/// Named rule: <c>path-without-operations</c>.
let pathWithoutOperationsRule: NamedRule = { Id = "path-without-operations"; Rule = pathWithoutOperations }

/// Named rule: <c>missing-content-type</c>.
let missingContentTypeRule: NamedRule = { Id = "missing-content-type"; Rule = missingContentType }

/// All built-in documentation and structure rules as named pairs.
let defaultNamedRules: NamedRule list = [
    missingOperationIdRule
    emptyOperationSummaryRule
    emptyParameterDescriptionRule
    emptySchemaPropertyDescriptionRule
    unusedSchemasRule
    missingResponseDescriptionRule
    pathWithoutOperationsRule
    missingContentTypeRule
]

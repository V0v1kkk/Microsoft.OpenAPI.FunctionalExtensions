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

let missingOperationId (document: OpenApiDocument) : LintViolation list =
    operationContexts document
    |> List.choose (fun (path, method, operation, _) ->
        match operationIdOption operation with
        | Some operationId when not (String.IsNullOrWhiteSpace operationId) -> None
        | _ ->
            Some(
                violation
                    "missingOperationId"
                    Error
                    "Operation must have a non-empty operationId."
                    (OperationLevel(path, httpMethodName method, operationIdOption operation))
            ))

let emptyOperationSummary (document: OpenApiDocument) : LintViolation list =
    operationContexts document
    |> List.choose (fun (path, method, operation, _) ->
        match AdapterCore.ofObj operation.Summary with
        | Some summary when not (String.IsNullOrWhiteSpace summary) -> None
        | _ ->
            Some(
                violation
                    "emptyOperationSummary"
                    Warning
                    "Operation should have a non-empty summary."
                    (OperationLevel(path, httpMethodName method, operationIdOption operation))
            ))

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
                        "emptyParameterDescription"
                        Warning
                        $"Parameter '{parameterName parameter}' should have a description."
                        (ParameterLevel(path, methodName, parameterName parameter))
                )))

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
                        "emptySchemaPropertyDescription"
                        Warning
                        $"Schema property '{propertyName}' should have a description."
                        (SchemaPropertyLevel(schemaName, propertyName))
                )))

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
                    "unusedSchemas"
                    Warning
                    $"Component schema '{schemaName}' is not referenced from any path or operation."
                    (SchemaLevel schemaName)
            ))

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
                        "missingResponseDescription"
                        Error
                        $"Response '{statusCode}' must have a description."
                        (OperationLevel(path, methodName, operationId))
                )))

let pathWithoutOperations (document: OpenApiDocument) : LintViolation list =
    DocumentAdapters.documentPaths document
    |> List.choose (fun (path, pathItem) ->
        match OperationAdapters.pathItemOperations pathItem with
        | [] ->
            Some(
                violation
                    "pathWithoutOperations"
                    Warning
                    $"Path '{path}' has no operations defined."
                    (PathLevel path)
            )
        | _ -> None)

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
                                "missingContentType"
                                Error
                                $"Response '{statusCode}' has a content section but no media types."
                                (OperationLevel(path, methodName, operationId))
                        )
                    else
                        None))

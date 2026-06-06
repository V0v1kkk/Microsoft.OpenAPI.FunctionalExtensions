[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation

open System
open System.Collections.Generic
open System.Net.Http
open System.Text.Json
open System.Text.Json.Nodes
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns

type private JsonValueKind =
    | JsonNull
    | JsonString
    | JsonInteger
    | JsonNumber
    | JsonBoolean
    | JsonArray
    | JsonObject
    | JsonUnknown

let private ruleName = "invalidExamples"

let private violation severity message location = {
    Rule = ruleName
    Severity = severity
    Message = message
    Location = location
}

let private isJsonNull (node: JsonNode) =
    isNull node || JsonNullSentinel.IsJsonNullSentinel node

let private classifyJsonNode (node: JsonNode) : JsonValueKind =
    match node with
    | null -> JsonNull
    | n when JsonNullSentinel.IsJsonNullSentinel n -> JsonNull
    | :? JsonArray -> JsonArray
    | :? JsonObject -> JsonObject
    | :? JsonValue as value ->
        match value.GetValueKind() with
        | JsonValueKind.String -> JsonString
        | JsonValueKind.Number ->
            let text = value.ToString()

            if text.Contains('.') || text.Contains('e') || text.Contains('E') then
                JsonNumber
            else
                JsonInteger
        | JsonValueKind.True
        | JsonValueKind.False -> JsonBoolean
        | JsonValueKind.Null -> JsonNull
        | _ -> JsonUnknown
    | _ -> JsonUnknown

let private hasTypeFlag (schemaType: Nullable<JsonSchemaType>) (flag: JsonSchemaType) =
    schemaType.HasValue && (schemaType.Value &&& flag) = flag

let private schemaAllowsNull (schema: IOpenApiSchema) =
    match schema with
    | null -> false
    | s -> SchemaAdapters.schemaIsNullable s

let private effectiveSchemaTypes (schema: IOpenApiSchema) : JsonSchemaType list =
    match schema with
    | null -> []
    | s ->
        match SchemaAdapters.schemaType s with
        | None -> []
        | Some schemaType ->
            [
                if hasTypeFlag (Nullable schemaType) JsonSchemaType.String then JsonSchemaType.String
                if hasTypeFlag (Nullable schemaType) JsonSchemaType.Integer then JsonSchemaType.Integer
                if hasTypeFlag (Nullable schemaType) JsonSchemaType.Number then JsonSchemaType.Number
                if hasTypeFlag (Nullable schemaType) JsonSchemaType.Boolean then JsonSchemaType.Boolean
                if hasTypeFlag (Nullable schemaType) JsonSchemaType.Array then JsonSchemaType.Array
                if hasTypeFlag (Nullable schemaType) JsonSchemaType.Object then JsonSchemaType.Object
            ]

let private inferSchemaTypes (schema: IOpenApiSchema) : JsonSchemaType list =
    match effectiveSchemaTypes schema with
    | [] ->
        match schema with
        | null -> []
        | ObjectSchema _ -> [ JsonSchemaType.Object ]
        | ArraySchema _ -> [ JsonSchemaType.Array ]
        | _ -> []
    | types -> types

let private jsonKindMatchesSchemaType (jsonKind: JsonValueKind) (schemaType: JsonSchemaType) =
    match jsonKind, schemaType with
    | JsonNull, _ -> false
    | JsonString, JsonSchemaType.String -> true
    | JsonInteger, JsonSchemaType.Integer -> true
    | JsonNumber, JsonSchemaType.Number -> true
    | JsonInteger, JsonSchemaType.Number -> true
    | JsonBoolean, JsonSchemaType.Boolean -> true
    | JsonArray, JsonSchemaType.Array -> true
    | JsonObject, JsonSchemaType.Object -> true
    | _ -> false

let private typeMismatchMessage (jsonKind: JsonValueKind) (schema: IOpenApiSchema) (path: string) =
    let expected =
        inferSchemaTypes schema
        |> List.map string
        |> String.concat " | "

    let actual =
        match jsonKind with
        | JsonNull -> "null"
        | JsonString -> "string"
        | JsonInteger -> "integer"
        | JsonNumber -> "number"
        | JsonBoolean -> "boolean"
        | JsonArray -> "array"
        | JsonObject -> "object"
        | JsonUnknown -> "unknown"

    $"Example at '{path}' has type '{actual}' but schema expects '{expected}'."

let private enumContainsValue (enumValues: JsonNode list) (example: JsonNode) =
    enumValues |> List.exists (fun enumValue -> JsonNode.DeepEquals(enumValue, example))

let private formatHintViolation (schema: IOpenApiSchema) (example: JsonNode) (path: string) =
    match SchemaAdapters.schemaFormat schema, classifyJsonNode example with
    | Some "date", JsonString ->
        let text = example.ToString().Trim('"')

        if not (DateOnly.TryParse(text) |> fst) then
            Some(
                violation
                    Info
                    $"Example at '{path}' with format 'date' does not look like a date."
                    DocumentLevel
            )
        else
            None
    | Some "email", JsonString ->
        let text = example.ToString().Trim('"')

        if not (text.Contains '@') then
            Some(
                violation
                    Info
                    $"Example at '{path}' with format 'email' does not look like an email."
                    DocumentLevel
            )
        else
            None
    | _ -> None

let rec private validateExampleCore
    (document: OpenApiDocument option)
    (schema: IOpenApiSchema)
    (example: JsonNode)
    (path: string)
    (visited: Set<string>)
    : LintViolation list =
    if isJsonNull example then
        if schemaAllowsNull schema then
            []
        else
            [
                violation
                    Error
                    $"Example at '{path}' is null but schema is not nullable."
                    DocumentLevel
            ]
    else
        match schema with
        | null -> []
        | unresolved ->
            match ReferenceAdapters.trySchemaReferenceId unresolved with
            | Some refName when Set.contains refName visited -> []
            | Some refName ->
                let visitedWithRef = Set.add refName visited

                match document with
                | Some doc ->
                    match DocumentAdapters.tryComponentSchema doc refName with
                    | None -> validateExampleCore document unresolved example path visitedWithRef
                    | Some resolved -> validateExampleCore document resolved example path visitedWithRef
                | None -> validateExampleCore document unresolved example path visitedWithRef
            | None ->
                match unresolved with
                | ComposedSchema(kind, branches) ->
                    match kind with
                    | CompositionKind.AllOf ->
                        branches
                        |> List.collect (fun branch -> validateExampleCore document branch example path visited)
                    | CompositionKind.OneOf
                    | CompositionKind.AnyOf ->
                        let branchResults =
                            branches
                            |> List.map (fun branch ->
                                validateExampleCore document branch example path visited)

                        if branchResults |> List.exists List.isEmpty then
                            []
                        else
                            [
                                violation
                                    Error
                                    $"Example at '{path}' does not match any {kind} schema branch."
                                    DocumentLevel
                            ]
                | _ ->
                    let jsonKind = classifyJsonNode example
                    let schemaTypes = inferSchemaTypes unresolved

                    let typeViolations =
                        if List.isEmpty schemaTypes then
                            []
                        elif schemaTypes |> List.exists (jsonKindMatchesSchemaType jsonKind) then
                            []
                        else
                            [
                                violation
                                    Error
                                    (typeMismatchMessage jsonKind unresolved path)
                                    DocumentLevel
                            ]

                    let enumViolations =
                        match SchemaAdapters.schemaEnum unresolved with
                        | [] -> []
                        | enumValues when enumContainsValue enumValues example -> []
                        | _ ->
                            [
                                violation
                                    Error
                                    $"Example at '{path}' is not one of the allowed enum values."
                                    DocumentLevel
                            ]

                    let objectViolations =
                        match example with
                        | :? JsonObject as jsonObject ->
                            let properties = SchemaAdapters.schemaProperties unresolved
                            let required = SchemaAdapters.schemaRequired unresolved

                            let missingRequired =
                                required
                                |> Set.toList
                                |> List.choose (fun propertyName ->
                                    if jsonObject.ContainsKey propertyName then
                                        None
                                    else
                                        Some(
                                            violation
                                                Error
                                                $"Example at '{path}' is missing required property '{propertyName}'."
                                                DocumentLevel
                                        ))

                            let unknownProperties =
                                if unresolved.AdditionalPropertiesAllowed then
                                    []
                                else
                                    jsonObject
                                    |> Seq.choose (fun (KeyValue (propertyName, _)) ->
                                        if Map.containsKey propertyName properties then
                                            None
                                        else
                                            Some(
                                                violation
                                                    Error
                                                    $"Example at '{path}' has unknown property '{propertyName}' but additionalProperties is false."
                                                    DocumentLevel
                                            ))
                                    |> Seq.toList

                            let propertyViolations =
                                properties
                                |> Map.toList
                                |> List.collect (fun (propertyName, propertySchema) ->
                                    match jsonObject.TryGetPropertyValue propertyName with
                                    | true, child when not (isNull child) ->
                                        validateExampleCore
                                            document
                                            propertySchema
                                            child
                                            $"{path}.{propertyName}"
                                            visited
                                    | _ -> [])

                            missingRequired @ unknownProperties @ propertyViolations
                        | _ -> []

                    let arrayViolations =
                        match example with
                        | :? JsonArray as jsonArray ->
                            let itemsSchema =
                                match unresolved with
                                | ArraySchema items -> Some items
                                | _ -> SchemaAdapters.schemaItems unresolved

                            match itemsSchema with
                            | None -> []
                            | Some items ->
                                jsonArray
                                |> Seq.mapi (fun index item ->
                                    validateExampleCore
                                        document
                                        items
                                        item
                                        $"{path}[{index}]"
                                        visited)
                                |> Seq.collect id
                                |> Seq.toList
                        | _ -> []

                    let formatViolation =
                        formatHintViolation unresolved example path |> Option.toList

                    typeViolations
                    @ enumViolations
                    @ objectViolations
                    @ arrayViolations
                    @ formatViolation

let validateExample (schema: IOpenApiSchema) (example: JsonNode) (path: string) : LintViolation list =
    validateExampleCore None schema example path Set.empty

let private validateExampleWithDocument
    (document: OpenApiDocument)
    (schema: IOpenApiSchema option)
    (example: JsonNode)
    (path: string)
    (location: LintLocation)
    : LintViolation list =
    match schema with
    | None -> []
    | Some resolvedSchema ->
        validateExampleCore (Some document) resolvedSchema example path Set.empty
        |> List.map (fun item -> { item with Location = location })

let private exampleNodesFromMediaType (mediaType: IOpenApiMediaType) : (string * JsonNode) list =
    match mediaType with
    | null -> []
    | mt ->
        let inlineExample =
            match AdapterCore.ofObj mt.Example with
            | None -> []
            | Some example -> [ "example", example ]

        let namedExamples =
            match AdapterCore.ofObj mt.Examples with
            | None -> []
            | Some examples ->
                examples
                |> Seq.choose (fun entry ->
                    match entry.Value with
                    | null -> None
                    | exampleObject ->
                        match AdapterCore.ofObj exampleObject.Value with
                        | None -> None
                        | Some value -> Some($"examples.{entry.Key}", value))
                |> Seq.toList

        inlineExample @ namedExamples

let private exampleNodesFromParameter (parameter: IOpenApiParameter) : (string * JsonNode) list =
    match parameter with
    | null -> []
    | param ->
        let inlineExample =
            match AdapterCore.ofObj param.Example with
            | None -> []
            | Some example -> [ "example", example ]

        let namedExamples =
            match AdapterCore.ofObj param.Examples with
            | None -> []
            | Some examples ->
                examples
                |> Seq.choose (fun entry ->
                    match entry.Value with
                    | null -> None
                    | exampleObject ->
                        match AdapterCore.ofObj exampleObject.Value with
                        | None -> None
                        | Some value -> Some($"examples.{entry.Key}", value))
                |> Seq.toList

        inlineExample @ namedExamples

let private httpMethodName (method: HttpMethod) = method.ToString()

let private operationIdOption (operation: OpenApiOperation) =
    AdapterCore.ofObj operation.OperationId

let private parameterName (parameter: IOpenApiParameter) =
    AdapterCore.ofObj parameter.Name |> Option.defaultValue "<unnamed>"

let private collectMediaTypeExamples
    (document: OpenApiDocument)
    (schemaSelector: IOpenApiMediaType -> IOpenApiSchema option)
    (mediaType: IOpenApiMediaType)
    (basePath: string)
    (location: LintLocation)
    : LintViolation list =
    exampleNodesFromMediaType mediaType
    |> List.collect (fun (examplePath, example) ->
        validateExampleWithDocument document (schemaSelector mediaType) example $"{basePath}.{examplePath}" location)

let invalidExamples (document: OpenApiDocument) : LintViolation list =
    let responseViolations =
        DocumentAdapters.allOperations document
        |> List.collect (fun (path, method, operation) ->
            let methodName = httpMethodName method
            let operationId = operationIdOption operation

            OperationAdapters.operationResponses operation
            |> Map.toList
            |> List.collect (fun (statusCode, response) ->
                match response with
                | null -> []
                | resp ->
                    let location = OperationLevel(path, methodName, operationId)

                    AdapterCore.readMap resp.Content
                    |> Map.toList
                    |> List.collect (fun (mediaTypeName, mediaType) ->
                        collectMediaTypeExamples
                            document
                            OperationAdapters.mediaTypeSchema
                            mediaType
                            $"paths.{path}.{methodName}.responses.{statusCode}.content.{mediaTypeName}"
                            location)))

    let requestBodyViolations =
        DocumentAdapters.allOperations document
        |> List.collect (fun (path, method, operation) ->
            let methodName = httpMethodName method
            let operationId = operationIdOption operation

            match OperationAdapters.operationRequestBody operation with
            | None -> []
            | Some requestBody ->
                let location = OperationLevel(path, methodName, operationId)

                AdapterCore.readMap requestBody.Content
                |> Map.toList
                |> List.collect (fun (mediaTypeName, mediaType) ->
                    collectMediaTypeExamples
                        document
                        OperationAdapters.mediaTypeSchema
                        mediaType
                        $"paths.{path}.{methodName}.requestBody.content.{mediaTypeName}"
                        location))

    let parameterViolations =
        DocumentAdapters.documentPaths document
        |> List.collect (fun (path, pathItem) ->
            let pathParameters = AdapterCore.readSeq pathItem.Parameters

            OperationAdapters.pathItemOperations pathItem
            |> List.collect (fun (method, operation) ->
                let methodName = httpMethodName method
                let operationId = operationIdOption operation

                (pathParameters @ OperationAdapters.operationParameters operation)
                |> List.collect (fun parameter ->
                    let parameterSchema =
                        match AdapterCore.ofObj parameter.Schema with
                        | Some schema -> Some schema
                        | None ->
                            match AdapterCore.ofObj parameter.Content with
                            | None -> None
                            | Some content ->
                                content.Values
                                |> Seq.tryPick OperationAdapters.mediaTypeSchema

                    let location =
                        ParameterLevel(path, methodName, parameterName parameter)

                    exampleNodesFromParameter parameter
                    |> List.collect (fun (examplePath, example) ->
                        validateExampleWithDocument
                            document
                            parameterSchema
                            example
                            $"paths.{path}.{methodName}.parameters.{parameterName parameter}.{examplePath}"
                            location))))

    let schemaViolations =
        DocumentAdapters.documentSchemas document
        |> Map.toList
        |> List.collect (fun (schemaName, schema) ->
            let location = SchemaLevel schemaName

            let inlineExample =
                match AdapterCore.ofObj schema.Example with
                | None -> []
                | Some example ->
                    validateExampleWithDocument
                        document
                        (Some schema)
                        example
                        $"components.schemas.{schemaName}.example"
                        location

            let arrayExamples =
                match schema.Examples with
                | null -> []
                | examples ->
                    examples
                    |> AdapterCore.readSeq
                    |> List.mapi (fun index example ->
                        validateExampleWithDocument
                            document
                            (Some schema)
                            example
                            $"components.schemas.{schemaName}.examples[{index}]"
                            location)
                    |> List.concat

            inlineExample @ arrayExamples)

    responseViolations @ requestBodyViolations @ parameterViolations @ schemaViolations

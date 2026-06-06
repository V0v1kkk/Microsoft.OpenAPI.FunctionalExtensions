module Microsoft.OpenAPI.FunctionalExtensions.Tests.LintingTests

open System
open System.Collections.Generic
open System.Net.Http
open Microsoft.OpenAPI.FunctionalExtensions
open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Linting
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

let private hasRule (ruleName: string) (violations: LintViolation list) =
    violations |> List.exists (fun violation -> violation.Rule = ruleName)

let private buildDocumentWithOperation
    (operationId: string option)
    (summary: string option)
    (parameterDescription: string option)
    (responseDescription: string option)
    (schemaNames: string list)
    (referencedSchemaName: string option)
    =
    let document = OpenApiDocument()

    let parameter = OpenApiParameter()
    parameter.Name <- "id"
    parameter.In <- ParameterLocation.Query
    parameter.Description <- parameterDescription |> Option.toObj

    let response = OpenApiResponse()
    response.Description <- responseDescription |> Option.toObj

    match referencedSchemaName with
    | Some schemaName ->
        let mediaType = OpenApiMediaType()
        mediaType.Schema <-
            OpenApiSchemaReference(referenceId = schemaName, hostDocument = document)
            :> IOpenApiSchema
        response.Content <- Dictionary<string, IOpenApiMediaType>() :> IDictionary<string, IOpenApiMediaType>
        response.Content.Add("application/json", mediaType) |> ignore
    | None -> ()

    let responses = OpenApiResponses()
    responses.Add("200", response) |> ignore

    let operation = OpenApiOperation()
    operation.OperationId <- operationId |> Option.toObj
    operation.Summary <- summary |> Option.toObj
    operation.Parameters <- List<IOpenApiParameter>([ parameter :> IOpenApiParameter ]) :> IList<IOpenApiParameter>
    operation.Responses <- responses

    let pathItem = OpenApiPathItem()
    pathItem.Operations <- Dictionary<HttpMethod, OpenApiOperation>()
    pathItem.Operations.Add(HttpMethod.Get, operation) |> ignore

    let paths = OpenApiPaths()
    paths.Add("/items", pathItem) |> ignore

    let schemas = Dictionary<string, IOpenApiSchema>()

    for schemaName in schemaNames do
        let propertySchema = OpenApiSchema()
        propertySchema.Type <- Nullable JsonSchemaType.String
        propertySchema.Description <- "A described property"

        let schema = OpenApiSchema()
        schema.Type <- Nullable JsonSchemaType.Object
        schema.Properties <- Dictionary<string, IOpenApiSchema>() :> IDictionary<string, IOpenApiSchema>
        schema.Properties.Add("value", propertySchema) |> ignore
        schemas.Add(schemaName, schema) |> ignore

    let components = OpenApiComponents()
    components.Schemas <- schemas

    document.Paths <- paths
    document.Components <- components
    document

let private buildCleanDocument () =
    let propertySchema = OpenApiSchema()
    propertySchema.Type <- Nullable JsonSchemaType.Integer
    propertySchema.Description <- "Pet identifier"

    let petSchema = OpenApiSchema()
    petSchema.Type <- Nullable JsonSchemaType.Object
    petSchema.Properties <- Dictionary<string, IOpenApiSchema>() :> IDictionary<string, IOpenApiSchema>
    petSchema.Properties.Add("id", propertySchema) |> ignore

    let schemas = Dictionary<string, IOpenApiSchema>()
    schemas.Add("Pet", petSchema) |> ignore

    let parameter = OpenApiParameter()
    parameter.Name <- "limit"
    parameter.In <- ParameterLocation.Query
    parameter.Description <- "Maximum number of pets to return"

    let response = OpenApiResponse()
    response.Description <- "A list of pets"

    let responses = OpenApiResponses()
    responses.Add("200", response) |> ignore

    let operation = OpenApiOperation()
    operation.OperationId <- "listPets"
    operation.Summary <- "List pets"
    operation.Parameters <- List<IOpenApiParameter>([ parameter :> IOpenApiParameter ]) :> IList<IOpenApiParameter>
    operation.Responses <- responses

    let pathItem = OpenApiPathItem()
    pathItem.Operations <- Dictionary<HttpMethod, OpenApiOperation>()
    pathItem.Operations.Add(HttpMethod.Get, operation) |> ignore

    let paths = OpenApiPaths()
    paths.Add("/pets", pathItem) |> ignore

    let components = OpenApiComponents()
    components.Schemas <- schemas

    let document = OpenApiDocument()
    document.Paths <- paths
    document.Components <- components
    document

[<Test>]
let ``petstore specification has lint violations`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Result.Error error -> Assert.Fail($"Failed to read petstore: %A{error}")
    | Result.Ok document ->
        let result = Linter.lintWithDefaults document
        Assert.That(result.Violations, Is.Not.Empty)

        Assert.That(
            hasRule "empty-schema-property-description" result.Violations,
            Is.True,
            "Expected missing schema property descriptions in petstore"
        )

[<Test>]
let ``missingOperationId catches operations without id`` () =
    let document =
        buildDocumentWithOperation None (Some "List items") (Some "Item id") (Some "OK") [ "Item" ] (Some "Item")

    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.missingOperationId document

    Assert.That(violations, Has.Length.EqualTo 1)
    Assert.That(violations.Head.Rule, Is.EqualTo "missing-operation-id")
    Assert.That(violations.Head.Severity, Is.EqualTo Severity.Error)

[<Test>]
let ``emptyOperationSummary catches empty summaries`` () =
    let document =
        buildDocumentWithOperation (Some "listItems") None (Some "Item id") (Some "OK") [ "Item" ] (Some "Item")

    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.emptyOperationSummary document

    Assert.That(violations, Has.Length.EqualTo 1)
    Assert.That(violations.Head.Rule, Is.EqualTo "empty-operation-summary")
    Assert.That(violations.Head.Severity, Is.EqualTo Severity.Warning)

[<Test>]
let ``unusedSchemas detects unreferenced component schemas`` () =
    let document =
        buildDocumentWithOperation (Some "listItems") (Some "List items") (Some "Item id") (Some "OK") [
            "Item"
            "Unused"
        ] (Some "Item")

    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.unusedSchemas document

    Assert.That(violations, Has.Length.EqualTo 1)
    Assert.That(violations.Head.Rule, Is.EqualTo "unused-schemas")

    match violations.Head.Location with
    | SchemaLevel schemaName -> Assert.That(schemaName, Is.EqualTo "Unused")
    | _ -> Assert.Fail("Expected schema-level location for unused schema")

[<Test>]
let ``clean specification has zero violations from documentation rules`` () =
    let document = buildCleanDocument ()

    let violations =
        [
            Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.missingOperationId document
            Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.emptyOperationSummary document
            Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.emptyParameterDescription document
            Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.emptySchemaPropertyDescription document
            Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.missingResponseDescription document
        ]
        |> List.concat

    Assert.That(violations, Is.Empty)

[<Test>]
let ``lintWithDefaults returns non-empty result for typical specification`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Result.Error error -> Assert.Fail($"Failed to read petstore: %A{error}")
    | Result.Ok document ->
        let result = Linter.lintWithDefaults document
        Assert.That(result.Violations, Is.Not.Empty)
        Assert.That(result.Violations.Length, Is.GreaterThan(3))

[<Test>]
let ``custom rule list applies only selected rules`` () =
    let document =
        buildDocumentWithOperation None None (Some "Item id") None [ "Item"; "Unused" ] (Some "Item")

    let result =
        Linter.lint
            [
                Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.missingOperationId
                Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.unusedSchemas
            ]
            document

    Assert.That(result.Violations, Has.Length.EqualTo 2)
    Assert.That(hasRule "missing-operation-id" result.Violations, Is.True)
    Assert.That(hasRule "unused-schemas" result.Violations, Is.True)
    Assert.That(hasRule "empty-operation-summary" result.Violations, Is.False)
    Assert.That(hasRule "empty-parameter-description" result.Violations, Is.False)

[<Test>]
let ``LinterConfig without disabled rules skips them`` () =
    let document =
        buildDocumentWithOperation (Some "listItems") None (Some "Item id") (Some "OK") [ "Item" ] (Some "Item")

    let enabled =
        Linter.lintWithConfig
            (LinterConfig.defaults |> LinterConfig.withOnly [ "empty-operation-summary" ])
            document

    let disabled =
        Linter.lintWithConfig
            (LinterConfig.defaults
             |> LinterConfig.withOnly [ "empty-operation-summary" ]
             |> LinterConfig.without [ "empty-operation-summary" ])
            document

    Assert.That(hasRule "empty-operation-summary" enabled.Violations, Is.True)
    Assert.That(disabled.Violations, Is.Empty)

[<Test>]
let ``LinterConfig withOnly runs only selected rules`` () =
    let document =
        buildDocumentWithOperation None None (Some "Item id") None [ "Item"; "Unused" ] (Some "Item")

    let result =
        Linter.lintWithConfig
            (LinterConfig.defaults |> LinterConfig.withOnly [ "missing-operation-id" ])
            document

    Assert.That(result.Violations, Has.Length.EqualTo 1)
    Assert.That(hasRule "missing-operation-id" result.Violations, Is.True)
    Assert.That(hasRule "unused-schemas" result.Violations, Is.False)
    Assert.That(hasRule "empty-operation-summary" result.Violations, Is.False)

[<Test>]
let ``LinterConfig withCustom executes custom rule`` () =
    let document = buildCleanDocument ()
    document.Info <- OpenApiInfo()

    let customRule (doc: OpenApiDocument) : LintViolation list =
        match doc.Info with
        | null -> []
        | info when String.IsNullOrWhiteSpace info.Title ->
            [ {
                Rule = "require-api-title"
                Severity = Error
                Message = "API title must be set."
                Location = DocumentLevel
              } ]
        | _ -> []

    let result =
        Linter.lintWithConfig (LinterConfig.defaults |> LinterConfig.withCustom [ customRule ]) document

    Assert.That(hasRule "require-api-title" result.Violations, Is.True)

let private buildDocumentWithDuplicateOperationIds () =
    let makeOperation operationId summary =
        let response = OpenApiResponse()
        response.Description <- "OK"

        let responses = OpenApiResponses()
        responses.Add("200", response) |> ignore

        let operation = OpenApiOperation()
        operation.OperationId <- operationId
        operation.Summary <- summary
        operation.Responses <- responses
        operation

    let pathItem = OpenApiPathItem()
    pathItem.Operations <- Dictionary<HttpMethod, OpenApiOperation>()
    pathItem.Operations.Add(HttpMethod.Get, makeOperation "sharedId" "First") |> ignore
    pathItem.Operations.Add(HttpMethod.Post, makeOperation "sharedId" "Second") |> ignore

    let paths = OpenApiPaths()
    paths.Add("/alpha", pathItem) |> ignore

    let document = OpenApiDocument()
    document.Paths <- paths
    document

let private buildDocumentWithMissingPathParameter () =
    let response = OpenApiResponse()
    response.Description <- "OK"

    let responses = OpenApiResponses()
    responses.Add("200", response) |> ignore

    let operation = OpenApiOperation()
    operation.OperationId <- "getItem"
    operation.Summary <- "Get item"
    operation.Responses <- responses

    let pathItem = OpenApiPathItem()
    pathItem.Operations <- Dictionary<HttpMethod, OpenApiOperation>()
    pathItem.Operations.Add(HttpMethod.Get, operation) |> ignore

    let paths = OpenApiPaths()
    paths.Add("/items/{itemId}", pathItem) |> ignore

    let document = OpenApiDocument()
    document.Paths <- paths
    document

let private buildDocumentWithoutResponses () =
    let operation = OpenApiOperation()
    operation.OperationId <- "noResponses"
    operation.Summary <- "No responses"

    let pathItem = OpenApiPathItem()
    pathItem.Operations <- Dictionary<HttpMethod, OpenApiOperation>()
    pathItem.Operations.Add(HttpMethod.Get, operation) |> ignore

    let paths = OpenApiPaths()
    paths.Add("/empty", pathItem) |> ignore

    let document = OpenApiDocument()
    document.Paths <- paths
    document

let private buildDocumentWithDuplicatePathParameters () =
    let duplicate = OpenApiParameter()
    duplicate.Name <- "itemId"
    duplicate.In <- ParameterLocation.Path
    duplicate.Description <- "First declaration"

    let duplicateAgain = OpenApiParameter()
    duplicateAgain.Name <- "itemId"
    duplicateAgain.In <- ParameterLocation.Path
    duplicateAgain.Description <- "Second declaration"

    let response = OpenApiResponse()
    response.Description <- "OK"

    let responses = OpenApiResponses()
    responses.Add("200", response) |> ignore

    let operation = OpenApiOperation()
    operation.OperationId <- "getItem"
    operation.Summary <- "Get item"
    operation.Parameters <-
        List<IOpenApiParameter>([ duplicateAgain :> IOpenApiParameter ]) :> IList<IOpenApiParameter>
    operation.Responses <- responses

    let pathItem = OpenApiPathItem()
    pathItem.Parameters <- List<IOpenApiParameter>([ duplicate :> IOpenApiParameter ]) :> IList<IOpenApiParameter>
    pathItem.Operations <- Dictionary<HttpMethod, OpenApiOperation>()
    pathItem.Operations.Add(HttpMethod.Get, operation) |> ignore

    let paths = OpenApiPaths()
    paths.Add("/items/{itemId}", pathItem) |> ignore

    let document = OpenApiDocument()
    document.Paths <- paths
    document

[<Test>]
let ``duplicateOperationId reports each duplicate occurrence`` () =
    let document = buildDocumentWithDuplicateOperationIds ()
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.duplicateOperationId document

    Assert.That(violations, Has.Length.EqualTo 2)
    Assert.That(violations |> List.forall (fun violation -> violation.Rule = "duplicate-operation-id"), Is.True)
    Assert.That(violations |> List.forall (fun violation -> violation.Severity = Error), Is.True)

[<Test>]
let ``pathParameterNotDefined catches missing path parameter declaration`` () =
    let document = buildDocumentWithMissingPathParameter ()
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.pathParameterNotDefined document

    Assert.That(violations, Has.Length.EqualTo 1)
    Assert.That(violations.Head.Rule, Is.EqualTo "path-parameter-not-defined")
    Assert.That(violations.Head.Message, Does.Contain("itemId"))

[<Test>]
let ``operationWithoutResponses catches operations with no responses`` () =
    let document = buildDocumentWithoutResponses ()
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.operationWithoutResponses document

    Assert.That(violations, Has.Length.EqualTo 1)
    Assert.That(violations.Head.Rule, Is.EqualTo "operation-without-responses")
    Assert.That(violations.Head.Severity, Is.EqualTo Error)

[<Test>]
let ``duplicatePathParameter catches repeated path parameter names`` () =
    let document = buildDocumentWithDuplicatePathParameters ()
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.duplicatePathParameter document

    Assert.That(violations, Has.Length.EqualTo 1)
    Assert.That(violations.Head.Rule, Is.EqualTo "duplicate-path-parameter")

    match violations.Head.Location with
    | ParameterLevel(_, _, parameterName) -> Assert.That(parameterName, Is.EqualTo "itemId")
    | _ -> Assert.Fail("Expected parameter-level location")

[<Test>]
let ``LinterConfig withSeverity overrides rule severity`` () =
    let document =
        buildDocumentWithOperation (Some "listItems") None (Some "Item id") (Some "OK") [ "Item" ] (Some "Item")

    let result =
        Linter.lintWithConfig
            (LinterConfig.defaults
             |> LinterConfig.withOnly [ "empty-operation-summary" ]
             |> LinterConfig.withSeverity "empty-operation-summary" Info)
            document

    Assert.That(result.Violations, Has.Length.EqualTo 1)
    Assert.That(result.Violations.Head.Severity, Is.EqualTo Info)

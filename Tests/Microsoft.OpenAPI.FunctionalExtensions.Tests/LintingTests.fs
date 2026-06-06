module Microsoft.OpenAPI.FunctionalExtensions.Tests.LintingTests

open System
open System.Collections.Generic
open System.Net.Http
open Microsoft.OpenAPI.FunctionalExtensions
open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
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
        let result = Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter.lintWithDefaults document
        Assert.That(result.Violations, Is.Not.Empty)

        Assert.That(
            hasRule "emptySchemaPropertyDescription" result.Violations,
            Is.True,
            "Expected missing schema property descriptions in petstore"
        )

[<Test>]
let ``missingOperationId catches operations without id`` () =
    let document =
        buildDocumentWithOperation None (Some "List items") (Some "Item id") (Some "OK") [ "Item" ] (Some "Item")

    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.missingOperationId document

    Assert.That(violations, Has.Length.EqualTo 1)
    Assert.That(violations.Head.Rule, Is.EqualTo "missingOperationId")
    Assert.That(violations.Head.Severity, Is.EqualTo Severity.Error)

[<Test>]
let ``emptyOperationSummary catches empty summaries`` () =
    let document =
        buildDocumentWithOperation (Some "listItems") None (Some "Item id") (Some "OK") [ "Item" ] (Some "Item")

    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.emptyOperationSummary document

    Assert.That(violations, Has.Length.EqualTo 1)
    Assert.That(violations.Head.Rule, Is.EqualTo "emptyOperationSummary")
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
    Assert.That(violations.Head.Rule, Is.EqualTo "unusedSchemas")

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
        let result = Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter.lintWithDefaults document
        Assert.That(result.Violations, Is.Not.Empty)
        Assert.That(result.Violations.Length, Is.GreaterThan(3))

[<Test>]
let ``custom rule list applies only selected rules`` () =
    let document =
        buildDocumentWithOperation None None (Some "Item id") None [ "Item"; "Unused" ] (Some "Item")

    let result =
        Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter.lint
            [
                Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.missingOperationId
                Microsoft.OpenAPI.FunctionalExtensions.Linting.Rules.unusedSchemas
            ]
            document

    Assert.That(result.Violations, Has.Length.EqualTo 2)
    Assert.That(hasRule "missingOperationId" result.Violations, Is.True)
    Assert.That(hasRule "unusedSchemas" result.Violations, Is.True)
    Assert.That(hasRule "emptyOperationSummary" result.Violations, Is.False)
    Assert.That(hasRule "emptyParameterDescription" result.Violations, Is.False)

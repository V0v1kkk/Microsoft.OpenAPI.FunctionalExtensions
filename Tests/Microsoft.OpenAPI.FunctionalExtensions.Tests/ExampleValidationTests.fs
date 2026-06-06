module Microsoft.OpenAPI.FunctionalExtensions.Tests.ExampleValidationTests

open System
open System.Collections.Generic
open System.Net.Http
open System.Text.Json.Nodes
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open NUnit.Framework

let private hasRule (ruleName: string) (violations: LintViolation list) =
    violations |> List.exists (fun violation -> violation.Rule = ruleName)

let private messageContains (fragment: string) (violations: LintViolation list) =
    violations |> List.exists (fun violation -> violation.Message.Contains fragment)

let private stringSchema () =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.String
    schema :> IOpenApiSchema

let private integerSchema () =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.Integer
    schema :> IOpenApiSchema

let private objectSchema (properties: (string * IOpenApiSchema) list) (required: string list) (additionalPropertiesAllowed: bool) =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.Object
    schema.Properties <- Dictionary<string, IOpenApiSchema>() :> IDictionary<string, IOpenApiSchema>

    for propertyName, propertySchema in properties do
        schema.Properties.Add(propertyName, propertySchema) |> ignore

    schema.Required <- HashSet<string>(required) :> ISet<string>
    schema.AdditionalPropertiesAllowed <- additionalPropertiesAllowed
    schema :> IOpenApiSchema

let private arraySchema (items: IOpenApiSchema) =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.Array
    schema.Items <- items
    schema :> IOpenApiSchema

let private enumSchema (values: string list) =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.String
    schema.Enum <-
        List<JsonNode>(values |> List.map (fun value -> JsonValue.Create value :> JsonNode))
        :> IList<JsonNode>
    schema :> IOpenApiSchema

let private nullableStringSchema () =
    let schema = OpenApiSchema()
    schema.Type <- Nullable(JsonSchemaType.String ||| JsonSchemaType.Null)
    schema :> IOpenApiSchema

[<Test>]
let ``string example matches string schema produces no violation`` () =
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample (stringSchema ()) (JsonValue.Create "hello") "test"
    Assert.That(violations, Is.Empty)

[<Test>]
let ``integer example against string schema produces violation`` () =
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample (stringSchema ()) (JsonValue.Create 42) "test"

    Assert.That(violations, Is.Not.Empty)
    Assert.That(violations.Head.Rule, Is.EqualTo "invalidExamples")
    Assert.That(violations.Head.Severity, Is.EqualTo Severity.Error)
    Assert.That(violations.Head.Message, Does.Contain "String")

[<Test>]
let ``object example with correct properties produces no violation`` () =
    let schema =
        objectSchema [ "id", integerSchema (); "name", stringSchema () ] [ "id"; "name" ] true

    let example = JsonNode.Parse("""{"id": 1, "name": "Fluffy"}""")
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema example "test"
    Assert.That(violations, Is.Empty)

[<Test>]
let ``object example with extra property and additionalProperties false produces violation`` () =
    let schema = objectSchema [ "value", stringSchema () ] [ "value" ] false
    let example = JsonNode.Parse("""{"value": "ok", "extra": true}""")
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema example "test"

    Assert.That(violations, Is.Not.Empty)
    Assert.That(messageContains "unknown property 'extra'" violations, Is.True)

[<Test>]
let ``object example missing required property produces violation`` () =
    let schema = objectSchema [ "id", integerSchema (); "name", stringSchema () ] [ "id"; "name" ] true
    let example = JsonNode.Parse("""{"id": 1}""")
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema example "test"

    Assert.That(violations, Is.Not.Empty)
    Assert.That(messageContains "missing required property 'name'" violations, Is.True)

[<Test>]
let ``array example with valid items produces no violation`` () =
    let schema = arraySchema (integerSchema ())
    let example = JsonNode.Parse("[1, 2, 3]")
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema example "test"
    Assert.That(violations, Is.Empty)

[<Test>]
let ``array example with invalid items produces violation`` () =
    let schema = arraySchema (integerSchema ())
    let example = JsonNode.Parse("[1, \"two\", 3]")
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema example "test"

    Assert.That(violations, Is.Not.Empty)
    Assert.That(messageContains "string" violations, Is.True)

[<Test>]
let ``enum value not in enum list produces violation`` () =
    let schema = enumSchema [ "red"; "green"; "blue" ]
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema (JsonValue.Create "purple") "test"

    Assert.That(violations, Is.Not.Empty)
    Assert.That(messageContains "enum" violations, Is.True)

[<Test>]
let ``null example against nullable schema produces no violation`` () =
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample (nullableStringSchema ()) JsonNullSentinel.JsonNull "test"
    Assert.That(violations, Is.Empty)

[<Test>]
let ``null example against non-nullable schema produces violation`` () =
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample (stringSchema ()) JsonNullSentinel.JsonNull "test"

    Assert.That(violations, Is.Not.Empty)
    Assert.That(messageContains "null" violations, Is.True)

[<Test>]
let ``nested object validation checks inner properties`` () =
    let innerSchema = objectSchema [ "count", integerSchema () ] [ "count" ] true
    let schema = objectSchema [ "inner", innerSchema ] [ "inner" ] true

    let validExample = JsonNode.Parse("""{"inner": {"count": 5}}""")
    Assert.That(Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema validExample "test", Is.Empty)

    let invalidExample = JsonNode.Parse("""{"inner": {}}""")
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema invalidExample "test"
    Assert.That(messageContains "missing required property 'count'" violations, Is.True)

let private buildDocumentWithResponseExample (schema: IOpenApiSchema) (example: JsonNode) =
    let mediaType = OpenApiMediaType()
    mediaType.Schema <- schema
    mediaType.Example <- example

    let response = OpenApiResponse()
    response.Description <- "OK"
    response.Content <- Dictionary<string, IOpenApiMediaType>() :> IDictionary<string, IOpenApiMediaType>
    response.Content.Add("application/json", mediaType) |> ignore

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
    paths.Add("/items", pathItem) |> ignore

    let document = OpenApiDocument()
    document.Paths <- paths
    document

[<Test>]
let ``response with example that mismatches schema type produces violation`` () =
    let document = buildDocumentWithResponseExample (stringSchema ()) (JsonValue.Create 123)
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.invalidExamples document

    Assert.That(violations, Is.Not.Empty)
    Assert.That(hasRule "invalidExamples" violations, Is.True)

let private buildDocumentWithParameterExample (schema: IOpenApiSchema) (example: JsonNode) =
    let parameter = OpenApiParameter()
    parameter.Name <- "limit"
    parameter.In <- ParameterLocation.Query
    parameter.Schema <- schema
    parameter.Example <- example

    let operation = OpenApiOperation()
    operation.OperationId <- "listItems"
    operation.Summary <- "List items"
    operation.Parameters <- List<IOpenApiParameter>([ parameter :> IOpenApiParameter ]) :> IList<IOpenApiParameter>
    operation.Responses <- OpenApiResponses()

    let pathItem = OpenApiPathItem()
    pathItem.Operations <- Dictionary<HttpMethod, OpenApiOperation>()
    pathItem.Operations.Add(HttpMethod.Get, operation) |> ignore

    let paths = OpenApiPaths()
    paths.Add("/items", pathItem) |> ignore

    let document = OpenApiDocument()
    document.Paths <- paths
    document

[<Test>]
let ``parameter with example that mismatches schema type produces violation`` () =
    let document = buildDocumentWithParameterExample (integerSchema ()) (JsonValue.Create "not-an-integer")
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.invalidExamples document

    Assert.That(violations, Is.Not.Empty)
    Assert.That(messageContains "string" violations, Is.True)

let private buildDocumentWithSchemaExample (schemaName: string) (schema: OpenApiSchema) =
    let schemas = Dictionary<string, IOpenApiSchema>()
    schemas.Add(schemaName, schema :> IOpenApiSchema) |> ignore

    let components = OpenApiComponents()
    components.Schemas <- schemas

    let document = OpenApiDocument()
    document.Components <- components
    document

[<Test>]
let ``schema-level example validation detects invalid enum`` () =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.String
    schema.Enum <- List<JsonNode>([ JsonValue.Create "red" :> JsonNode ]) :> IList<JsonNode>
    schema.Example <- JsonValue.Create "purple"

    let document = buildDocumentWithSchemaExample "Color" schema
    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.invalidExamples document

    Assert.That(violations, Is.Not.Empty)
    Assert.That(messageContains "enum" violations, Is.True)

[<Test>]
let ``document with no examples produces no violations from this rule`` () =
    let operation = OpenApiOperation()
    operation.OperationId <- "noop"
    operation.Summary <- "No examples"
    operation.Responses <- OpenApiResponses()

    let pathItem = OpenApiPathItem()
    pathItem.Operations <- Dictionary<HttpMethod, OpenApiOperation>()
    pathItem.Operations.Add(HttpMethod.Get, operation) |> ignore

    let paths = OpenApiPaths()
    paths.Add("/noop", pathItem) |> ignore

    let document = OpenApiDocument()
    document.Paths <- paths

    let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.invalidExamples document
    Assert.That(violations, Is.Empty)

[<Test>]
let ``examples validation specification contains expected violations`` () =
    match readSpecification "Specifications/examples-validation.yaml" with
    | Result.Error error -> Assert.Fail($"Failed to read examples-validation.yaml: %A{error}")
    | Result.Ok document ->
        let violations = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.invalidExamples document

        Assert.That(violations, Is.Not.Empty)
        Assert.That(hasRule "invalidExamples" violations, Is.True)
        Assert.That(messageContains "integer" violations, Is.True)

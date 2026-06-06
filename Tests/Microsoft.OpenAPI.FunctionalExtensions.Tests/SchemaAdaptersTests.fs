module Microsoft.OpenAPI.FunctionalExtensions.Tests.SchemaAdapters

open System
open System.Collections.Generic
open System.Net.Http
open System.Text.Json.Nodes
open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions

[<Test>]
let ``schemaProperties returns empty map when properties are null`` () =
    let schema = OpenApiSchema(Properties = null) :> IOpenApiSchema
    Assert.That(Map.isEmpty (SchemaAdapters.schemaProperties schema), Is.True)

[<Test>]
let ``schemaProperties skips null property values`` () =
    let properties = Dictionary<string, IOpenApiSchema>()
    properties.Add("keep", OpenApiSchema(Type = Nullable JsonSchemaType.String))
    properties.Add("skip", null)

    let schema =
        OpenApiSchema(Properties = properties) :> IOpenApiSchema

    let result = SchemaAdapters.schemaProperties schema
    Assert.That(result.Count, Is.EqualTo(1))
    Assert.That(result.ContainsKey "keep", Is.True)

[<Test>]
let ``schemaIsNullable is true when Null flag is present`` () =
    let schema =
        OpenApiSchema(Type = Nullable(JsonSchemaType.String ||| JsonSchemaType.Null))
        :> IOpenApiSchema

    Assert.That(SchemaAdapters.schemaIsNullable schema, Is.True)

[<Test>]
let ``schemaIsNullable is false for non-nullable schema`` () =
    let schema =
        OpenApiSchema(Type = Nullable JsonSchemaType.Integer) :> IOpenApiSchema

    Assert.That(SchemaAdapters.schemaIsNullable schema, Is.False)

[<Test>]
let ``trySchemaReferenceId returns Some for schema reference`` () =
    let schema = OpenApiSchemaReference("Pet", null, null) :> IOpenApiSchema
    Assert.That(ReferenceAdapters.trySchemaReferenceId schema, Is.EqualTo(Some "Pet"))

[<Test>]
let ``trySchemaReferenceId returns None for non-reference schema`` () =
    let schema =
        OpenApiSchema(Type = Nullable JsonSchemaType.String) :> IOpenApiSchema

    Assert.That(ReferenceAdapters.trySchemaReferenceId schema, Is.EqualTo(None))

[<Test>]
let ``trySchemaReferenceId parses ReferenceV3 when Id is empty`` () =
    let schema =
        OpenApiSchemaReference("#/components/schemas/Pet", null, null)
        :> IOpenApiSchema

    Assert.That(ReferenceAdapters.trySchemaReferenceId schema, Is.EqualTo(Some "Pet"))

[<Test>]
let ``referencePointer formats component schema pointer`` () =
    Assert.That(ReferenceAdapters.referencePointer "Pet", Is.EqualTo("#/components/schemas/Pet"))

[<Test>]
let ``tryResolveSchemaReference resolves component schema from document`` () =
    let petSchema = OpenApiSchema(Title = "PetModel") :> IOpenApiSchema
    let schemas = Dictionary<string, IOpenApiSchema>()
    schemas.Add("Pet", petSchema)

    let document =
        OpenApiDocument(Components = OpenApiComponents(Schemas = schemas))

    let reference = OpenApiSchemaReference("Pet", document, null) :> IOpenApiSchema

    match ReferenceAdapters.tryResolveSchemaReference document reference with
    | None -> Assert.Fail("Expected reference to resolve")
    | Some resolved -> Assert.That(SchemaAdapters.schemaTitle resolved, Is.EqualTo(Some "PetModel"))

[<Test>]
let ``tryResolveSchemaReference returns schema as-is when not a reference`` () =
    let schema =
        OpenApiSchema(Type = Nullable JsonSchemaType.Boolean) :> IOpenApiSchema

    let document = OpenApiDocument()

    match ReferenceAdapters.tryResolveSchemaReference document schema with
    | None -> Assert.Fail("Expected non-reference schema to be returned")
    | Some same -> Assert.That(same, Is.SameAs(schema))

[<Test>]
let ``isUnresolvedReference is true for unresolved schema reference`` () =
    let schema = OpenApiSchemaReference("Missing", null, null) :> IOpenApiSchema
    Assert.That(ReferenceAdapters.isUnresolvedReference schema, Is.True)

[<Test>]
let ``isUnresolvedReference is false for plain schema`` () =
    let schema =
        OpenApiSchema(Type = Nullable JsonSchemaType.String) :> IOpenApiSchema

    Assert.That(ReferenceAdapters.isUnresolvedReference schema, Is.False)

[<Test>]
let ``operationParameters returns empty list when parameters are null`` () =
    let operation = OpenApiOperation(Parameters = null)
    Assert.That(OperationAdapters.operationParameters operation, Is.Empty)

[<Test>]
let ``schemasFromContent returns empty list for null content`` () =
    let content: IDictionary<string, IOpenApiMediaType> = null
    Assert.That(OperationAdapters.schemasFromContent content, Is.Empty)

[<Test>]
let ``schemasFromContent returns empty list for empty content`` () =
    let content = Dictionary<string, IOpenApiMediaType>()
    Assert.That(OperationAdapters.schemasFromContent content, Is.Empty)

[<Test>]
let ``schemasFromContent collects schemas from media types`` () =
    let bodySchema =
        OpenApiSchema(Type = Nullable JsonSchemaType.Object) :> IOpenApiSchema

    let content = Dictionary<string, IOpenApiMediaType>()
    content.Add("application/json", OpenApiMediaType(Schema = bodySchema))

    let schemas = OperationAdapters.schemasFromContent content
    Assert.That(schemas.Length, Is.EqualTo(1))
    Assert.That(schemas.[0], Is.SameAs(bodySchema))

[<Test>]
let ``schemasFromOperation collects schemas from parameters request and responses`` () =
    let parameterSchema =
        OpenApiSchema(Type = Nullable JsonSchemaType.Integer) :> IOpenApiSchema

    let requestSchema =
        OpenApiSchema(Type = Nullable JsonSchemaType.Object) :> IOpenApiSchema

    let responseSchema =
        OpenApiSchema(Type = Nullable JsonSchemaType.String) :> IOpenApiSchema

    let requestContent = Dictionary<string, IOpenApiMediaType>()
    requestContent.Add("application/json", OpenApiMediaType(Schema = requestSchema))

    let responseContent = Dictionary<string, IOpenApiMediaType>()
    responseContent.Add("application/json", OpenApiMediaType(Schema = responseSchema))

    let parameters = ResizeArray<IOpenApiParameter>()
    parameters.Add(OpenApiParameter(Name = "limit", Schema = parameterSchema) :> IOpenApiParameter)

    let responses = OpenApiResponses()
    responses.Add("200", OpenApiResponse(Content = responseContent) :> IOpenApiResponse)

    let operation =
        OpenApiOperation(
            Parameters = parameters,
            RequestBody = OpenApiRequestBody(Content = requestContent),
            Responses = responses
        )

    let schemas = OperationAdapters.schemasFromOperation operation
    Assert.That(schemas.Length, Is.EqualTo(3))
    Assert.That(schemas |> List.exists (fun s -> Object.ReferenceEquals(s, parameterSchema)), Is.True)
    Assert.That(schemas |> List.exists (fun s -> Object.ReferenceEquals(s, requestSchema)), Is.True)
    Assert.That(schemas |> List.exists (fun s -> Object.ReferenceEquals(s, responseSchema)), Is.True)

[<Test>]
let ``pathItemOperations returns operations from path item`` () =
    let operations = Dictionary<HttpMethod, OpenApiOperation>()
    let getOperation = OpenApiOperation(OperationId = "listPets")
    operations.Add(HttpMethod.Get, getOperation)

    let pathItem = OpenApiPathItem(Operations = operations) :> IOpenApiPathItem
    let result = OperationAdapters.pathItemOperations pathItem

    Assert.That(result.Length, Is.EqualTo(1))
    Assert.That(fst result.[0], Is.EqualTo(HttpMethod.Get))
    Assert.That(snd result.[0], Is.SameAs(getOperation))

[<Test>]
let ``operationTags returns tag names null-safely`` () =
    let tags = HashSet<OpenApiTagReference>()
    tags.Add(OpenApiTagReference("pets")) |> ignore
    tags.Add(null) |> ignore
    tags.Add(OpenApiTagReference("store")) |> ignore

    let operation = OpenApiOperation(Tags = tags)

    Assert.That(OperationAdapters.operationTags operation = [ "pets"; "store" ], Is.True)

[<Test>]
let ``schemaEnum skips null values`` () =
    let enumValues = ResizeArray<JsonNode>()
    enumValues.Add(JsonValue.Create("active"))
    enumValues.Add(null)
    enumValues.Add(JsonValue.Create("inactive"))

    let schema = OpenApiSchema(Enum = enumValues) :> IOpenApiSchema
    Assert.That(List.length (SchemaAdapters.schemaEnum schema), Is.EqualTo(2))

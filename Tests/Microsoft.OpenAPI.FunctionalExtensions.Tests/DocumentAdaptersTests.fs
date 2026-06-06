module Microsoft.OpenAPI.FunctionalExtensions.Tests.DocumentAdaptersTests

open System.Collections.Generic
open System.Net.Http
open System.Text.Json.Nodes
open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open StringExtensions

[<Test>]
let ``documentSchemas returns empty map when components are null`` () =
    let document = OpenApiDocument(Components = null)
    Assert.That(Map.isEmpty (DocumentAdapters.documentSchemas document), Is.True)

[<Test>]
let ``documentSchemas returns populated schemas from components`` () =
    let petSchema = OpenApiSchema(Title = "PetModel") :> IOpenApiSchema
    let errorSchema = OpenApiSchema(Title = "ErrorModel") :> IOpenApiSchema

    let schemas = Dictionary<string, IOpenApiSchema>()
    schemas.Add("Pet", petSchema)
    schemas.Add("Error", errorSchema)

    let document =
        OpenApiDocument(Components = OpenApiComponents(Schemas = schemas))

    let result = DocumentAdapters.documentSchemas document
    Assert.That(result.Count, Is.EqualTo(2))
    Assert.That(result.ContainsKey "Pet", Is.True)
    Assert.That(result.ContainsKey "Error", Is.True)
    Assert.That(SchemaAdapters.schemaTitle result.["Pet"], Is.EqualTo(Some "PetModel"))

[<Test>]
let ``documentTags returns empty list when tags are null`` () =
    let document = OpenApiDocument(Tags = null)
    Assert.That(DocumentAdapters.documentTags document, Is.Empty)

[<Test>]
let ``tryComponentSchema returns Some for existing schema and None for missing`` () =
    let petSchema = OpenApiSchema(Title = "PetModel") :> IOpenApiSchema
    let schemas = Dictionary<string, IOpenApiSchema>()
    schemas.Add("Pet", petSchema)

    let document =
        OpenApiDocument(Components = OpenApiComponents(Schemas = schemas))

    match DocumentAdapters.tryComponentSchema document "Pet" with
    | None -> Assert.Fail("Expected Pet schema to exist")
    | Some schema -> Assert.That(SchemaAdapters.schemaTitle schema, Is.EqualTo(Some "PetModel"))

    Assert.That(DocumentAdapters.tryComponentSchema document "Missing", Is.EqualTo(None))

[<Test>]
let ``documentPaths returns petstore paths from specification file`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Error error -> Assert.Fail($"Failed to read petstore: %A{error}")
    | Ok document ->
        let paths =
            DocumentAdapters.documentPaths document
            |> List.map fst
            |> List.sort

        Assert.That(paths = [ "/pets"; "/pets/{petId}" ], Is.True)

[<Test>]
let ``allOperations returns expected count from petstore`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Error error -> Assert.Fail($"Failed to read petstore: %A{error}")
    | Ok document ->
        let operations = DocumentAdapters.allOperations document
        Assert.That(operations.Length, Is.EqualTo(3))

        let operationIds =
            operations
            |> List.map (fun (_, _, operation) -> operation.OperationId)
            |> List.sort

        Assert.That(operationIds = [ "createPets"; "listPets"; "showPetById" ], Is.True)

[<Test>]
let ``tryExtensionString returns existing extension value`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Error error -> Assert.Fail($"Failed to read petstore: %A{error}")
    | Ok document ->
        let listPetsOperation =
            DocumentAdapters.allOperations document
            |> List.find (fun (_, method, operation) ->
                method = HttpMethod.Get && operation.OperationId = "listPets")

        let _, _, operation = listPetsOperation

        match ExtensionAdapters.tryExtensionString operation.Extensions "x-feature-flag" with
        | None -> Assert.Fail("Expected x-feature-flag extension to exist")
        | Some value -> Assert.That(value.icompare "true", Is.True)

[<Test>]
let ``extensionIsTruthy recognizes truthy extension values`` () =
    let truthyExtensions = Dictionary<string, IOpenApiExtension>()
    truthyExtensions.Add("x-enabled", JsonNodeExtension(JsonValue.Create(true)))
    truthyExtensions.Add("x-one", JsonNodeExtension(JsonValue.Create("1")))
    truthyExtensions.Add("x-yes", JsonNodeExtension(JsonValue.Create("yes")))
    truthyExtensions.Add("x-false", JsonNodeExtension(JsonValue.Create(false)))

    Assert.That(ExtensionAdapters.extensionIsTruthy truthyExtensions "x-enabled", Is.True)
    Assert.That(ExtensionAdapters.extensionIsTruthy truthyExtensions "x-one", Is.True)
    Assert.That(ExtensionAdapters.extensionIsTruthy truthyExtensions "x-yes", Is.True)
    Assert.That(ExtensionAdapters.extensionIsTruthy truthyExtensions "x-false", Is.False)
    Assert.That(ExtensionAdapters.extensionIsTruthy truthyExtensions "x-missing", Is.False)


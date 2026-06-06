module Microsoft.OpenAPI.FunctionalExtensions.Tests.MultiVersionReading

open System
open System.Net.Http
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open NUnit.Framework

let private hasNullType (schemaType: Nullable<JsonSchemaType>) =
  schemaType.HasValue
  && (schemaType.Value &&& JsonSchemaType.Null) = JsonSchemaType.Null

let private hasStringType (schemaType: Nullable<JsonSchemaType>) =
  schemaType.HasValue
  && (schemaType.Value &&& JsonSchemaType.String) = JsonSchemaType.String

[<Test>]
let ``OpenAPI 3.0 petstore reads and exposes nullable properties`` () =
    match readSpecification "Specifications/petstore-v30.yaml" with
    | Error err -> Assert.Fail($"Failed to read petstore-v30.yaml: %A{err}")
    | Ok document ->
        Assert.That(document.Paths.Count, Is.EqualTo(3))

        let petSchema = document.Components.Schemas.["Pet"]
        let tagSchema = petSchema.Properties.["tag"]
        Assert.That(hasNullType tagSchema.Type, Is.True)
        Assert.That(hasStringType tagSchema.Type, Is.True)

        let createPet =
            document.Paths.["/pets"].Operations.[HttpMethod.Post]
        Assert.That(createPet.RequestBody, Is.Not.Null)

[<Test>]
let ``OpenAPI 3.1 petstore reads and exposes JSON Schema 2020-12 features`` () =
    match readSpecification "Specifications/petstore-v31.yaml" with
    | Error err -> Assert.Fail($"Failed to read petstore-v31.yaml: %A{err}")
    | Ok document ->
        Assert.That(document.Webhooks.Count, Is.EqualTo(1))
        Assert.That(document.Components.PathItems.ContainsKey("PetCreatedWebhook"), Is.True)

        let petEvent = document.Components.Schemas.["PetEvent"]
        Assert.That(petEvent.Properties.["type"].Const, Is.EqualTo("pet.created"))
        Assert.That(petEvent.Definitions.ContainsKey("PetId"), Is.True)

        let tagSchema = document.Components.Schemas.["Pet"].Properties.["tag"]
        Assert.That(hasNullType tagSchema.Type, Is.True)

        let locationTuple = document.Components.Schemas.["LocationTuple"]
        Assert.That(locationTuple.UnrecognizedKeywords.ContainsKey("prefixItems"), Is.True)

[<Test>]
let ``OpenAPI 3.2 petstore reads and exposes 3.2-specific features`` () =
    match readSpecification "Specifications/petstore-v32.yaml" with
    | Error err -> Assert.Fail($"Failed to read petstore-v32.yaml: %A{err}")
    | Ok document ->
        Assert.That(document.Components.MediaTypes.ContainsKey("PetEventJsonl"), Is.True)

        let detailTag =
            document.Tags
            |> Seq.find (fun tag -> tag.Name = "pets/detail")

        Assert.That(detailTag.Summary, Is.EqualTo("Pet detail"))
        Assert.That(detailTag.Kind, Is.EqualTo("nav"))
        Assert.That(isNull (box detailTag.Parent), Is.False)

        let listPets =
            document.Paths.["/pets"].Operations.[HttpMethod.Get]
        let queryStringParam = listPets.Parameters |> Seq.head
        Assert.That(queryStringParam.In, Is.EqualTo(ParameterLocation.QueryString))

        match document.Components.SecuritySchemes.["OAuthDeviceFlow"] with
        | :? OpenApiSecurityScheme as scheme ->
            Assert.That(scheme.OAuth2MetadataUrl, Is.Not.Null)
            Assert.That(isNull scheme.Flows.DeviceAuthorization, Is.False)
        | _ -> Assert.Fail("Expected concrete OpenApiSecurityScheme for OAuthDeviceFlow")

        let petEvent = document.Components.Schemas.["PetEvent"]
        Assert.That(petEvent.Properties.["type"].Const, Is.EqualTo("pet.created"))
        Assert.That(petEvent.Definitions.ContainsKey("PetId"), Is.True)

        let locationTuple = document.Components.Schemas.["LocationTuple"]
        Assert.That(locationTuple.UnrecognizedKeywords.ContainsKey("prefixItems"), Is.True)

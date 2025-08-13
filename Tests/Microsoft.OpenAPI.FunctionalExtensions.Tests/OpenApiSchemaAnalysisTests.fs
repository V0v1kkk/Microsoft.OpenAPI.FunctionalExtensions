module Microsoft.OpenAPI.FunctionalExtensions.Tests.OpenApiSchemaAnalysisTests

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions
open OpenApiSchemaAnalysis

[<Test>]
let ``classify array of CartItem and unwrap to element`` () =
    match readSpecification "Specifications/e-shop-example.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let order = doc.Components.Schemas.["Order"]
        let itemsSchema = order.Properties.["items"]
        TestContext.WriteLine($"itemsSchema CLR type: {itemsSchema.GetType().FullName}")
        TestContext.WriteLine($"itemsSchema.Id: '{itemsSchema.Id}'")
        match classifySchema itemsSchema with
        | Array (Some el) ->
            TestContext.WriteLine($"element CLR type: {el.GetType().FullName}")
            TestContext.WriteLine($"element.Id: '{el.Id}'")
            // element must be reference to CartItem
            let elId = tryGetReferenceId el |> Option.defaultValue ""
            Assert.That(elId, Is.EqualTo("CartItem"))
            let (unwrapped, dims) = unwrapArrays itemsSchema
            Assert.That(dims, Is.EqualTo(1))
            let refId = tryGetReferenceId unwrapped |> Option.defaultValue ""
            Assert.That(refId, Is.EqualTo("CartItem"))
        | other -> Assert.Fail($"Unexpected classification: %A{other}")

[<Test>]
let ``format type label for arrays as array[Type]`` () =
    match readSpecification "Specifications/e-shop-example.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let order = doc.Components.Schemas.["Order"]
        let itemsSchema = order.Properties.["items"]
        let (unwrapped, dims) = unwrapArrays itemsSchema
        Assert.That(dims, Is.EqualTo(1))
        let id = tryGetReferenceId unwrapped |> Option.defaultValue ""
        Assert.That(id, Is.EqualTo("CartItem"))



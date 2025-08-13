module Microsoft.OpenAPI.FunctionalExtensions.Tests.Reading

open Microsoft.OpenAPI.FunctionalExtensions.Readers.Types
open Microsoft.OpenApi
open NUnit.Framework
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open ResultEx

[<SetUp>]
let Setup () =
    ()

[<Test>]
let ``Simple Petstore specification must read without errors`` () =
    let specificationPath = "Specifications/petstore.yaml"
    let readSpecificationResult = readSpecification specificationPath
    
    match readSpecificationResult with
    | Ok document -> Assert.That(document.Paths.Count, Is.GreaterThan(0))
    | Error _ -> Assert.Fail()

[<Test>]
let ``Operation extension boolean is parsed via tryGetValue`` () =
    let specificationPath = "Specifications/petstore.yaml"
    match readSpecification specificationPath with
    | Ok document ->
        let anyOp =
            document.Paths
            |> Seq.collect (fun kv -> kv.Value.Operations.Values)
            |> Seq.head
        let v = OpenApiTools.getExtensionValue anyOp.Extensions "x-feature-flag"
        Assert.That(v.IsSome, Is.True)
        Assert.That(v.Value = "true" || v.Value = "false", Is.True)
    | Error _ -> Assert.Fail()
module Microsoft.OpenAPI.FunctionalExtensions.Tests

open Microsoft.OpenAPI.FunctionalExtensions.Readers.Types
open Microsoft.OpenApi.Models
open NUnit.Framework
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools

[<SetUp>]
let Setup () =
    ()

[<Test>]
let ``Simple Petstore specification must read without errors`` () =
    let specificationPath = "Specifications/petstore.yaml"
    let readSpecificationResult = readSpecification specificationPath
    
    match readSpecificationResult with
    | Results.Success document -> Assert.NotZero document.Paths.Count
    | Results.Failure _ -> Assert.Fail()
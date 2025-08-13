module Microsoft.OpenAPI.FunctionalExtensions.Tests.OperationsTraversal

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiOperationsTraversal
open ResultEx

[<Test>]
let ``Collect route map from petstore`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Ok doc ->
        let m = collectRouteMap doc
        Assert.That(m.Routes.Count, Is.GreaterThan(0))
        Assert.That(m.Routes |> Seq.exists (fun r -> r.ParameterSchemas.Length >= 0), Is.True)
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")



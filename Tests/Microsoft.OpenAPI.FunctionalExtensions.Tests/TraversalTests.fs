module Microsoft.OpenAPI.FunctionalExtensions.Tests.Traversal

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiTraversal
open ResultEx

[<Test>]
let ``Collect schema graph from petstore components`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Ok doc ->
        let g = collectDocumentSchemas doc
        Assert.That(g.Nodes.Count, Is.GreaterThan(0))
        Assert.That(g.Edges.Count, Is.GreaterThanOrEqualTo(0))
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")



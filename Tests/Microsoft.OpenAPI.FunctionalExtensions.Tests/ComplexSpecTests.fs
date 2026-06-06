module Microsoft.OpenAPI.FunctionalExtensions.Tests.ComplexSpecTests

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors
open OpenApiTraversal
open OpenApiOperationsTraversal

let private mediumComplexSpec = "Specifications/medium-complex.yaml"
let private petstoreSpec = "Samples/petstore.yaml"

let private loadMediumComplex () =
    match readSpecification mediumComplexSpec with
    | Ok doc -> doc
    | Error e -> failwith $"Failed to read medium-complex spec: %A{e}"

[<Test>]
let ``medium-complex spec loads successfully`` () =
    let doc = loadMediumComplex ()
    Assert.That(doc.Paths.Count, Is.GreaterThanOrEqualTo(20))
    Assert.That(doc.Components.Schemas.Count, Is.GreaterThanOrEqualTo(30))

let private isComponentRootNode (nodeId: string) =
    nodeId.StartsWith "#/components/schemas/"
    && not (nodeId.Contains "/properties/")
    && not (nodeId.Contains "/allOf/")
    && not (nodeId.Contains "/oneOf/")
    && not (nodeId.Contains "/anyOf/")
    && not (nodeId.Contains "/items")
    && not (nodeId.Contains "/additionalProperties")

[<Test>]
let ``medium-complex schema graph has expected node count range`` () =
    let doc = loadMediumComplex ()
    let graph = collectDocumentSchemas doc

    let componentRootCount =
        graph.Nodes
        |> Seq.filter (fun node -> isComponentRootNode node.Id)
        |> Seq.distinctBy (fun node -> node.Id)
        |> Seq.length

    Assert.That(componentRootCount, Is.InRange(30, 50))
    Assert.That(graph.Nodes.Count, Is.LessThan(1000))

[<Test>]
let ``medium-complex route map has at least 20 routes`` () =
    let doc = loadMediumComplex ()
    let routeMap = collectRouteMap doc
    Assert.That(routeMap.Routes.Count, Is.GreaterThanOrEqualTo(20))

[<Test>]
let ``scissors with products tag produces subset from medium-complex`` () =
    let doc = loadMediumComplex ()
    let opts = { ScissorsOptions.Empty with IncludeTags = [ "products" ] }
    let cut = cutDocument doc opts

    Assert.That(cut.Paths.ContainsKey("/products"), Is.True)
    Assert.That(cut.Paths.ContainsKey("/products/{productId}"), Is.True)
    Assert.That(cut.Paths.ContainsKey("/users"), Is.False)
    Assert.That(cut.Components.Schemas.ContainsKey("Product"), Is.True)

[<Test>]
let ``merge medium-complex with petstore produces combined result`` () =
    match mergeFiles [ mediumComplexSpec; petstoreSpec ] with
    | Error e -> Assert.Fail($"Failed to merge specs: %A{e}")
    | Ok doc ->
        let hasMediumPath = not (isNull doc.Paths) && doc.Paths.ContainsKey("/orders")
        let hasPetstorePath = not (isNull doc.Paths) && doc.Paths.ContainsKey("/pets")
        let hasMediumSchema =
            not (isNull doc.Components)
            && not (isNull doc.Components.Schemas)
            && doc.Components.Schemas.ContainsKey("Order")
        let hasPetstoreSchema =
            not (isNull doc.Components)
            && not (isNull doc.Components.Schemas)
            && doc.Components.Schemas.ContainsKey("Pet")

        Assert.That(hasMediumPath, Is.True)
        Assert.That(hasPetstorePath, Is.True)
        Assert.That(hasMediumSchema, Is.True)
        Assert.That(hasPetstoreSchema, Is.True)

module Microsoft.OpenAPI.FunctionalExtensions.Tests.Traversal

open System
open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiTraversal
open ResultEx

let private hasCompositionEdge kind (fromContains: string) (toContains: string) (graph: SchemaGraph) =
    graph.Edges
    |> Seq.exists (fun edge ->
        match edge.EdgeKind with
        | Composition edgeKind ->
            edgeKind = kind
            && edge.FromId.Contains(fromContains, StringComparison.Ordinal)
            && edge.ToId.Contains(toContains, StringComparison.Ordinal)
        | _ -> false)

let private hasMapValueEdge (fromContains: string) (graph: SchemaGraph) =
    graph.Edges
    |> Seq.exists (fun edge ->
        match edge.EdgeKind with
        | MapValue -> edge.FromId.Contains(fromContains, StringComparison.Ordinal)
        | _ -> false)

[<Test>]
let ``medium-complex schema graph has AllOf OneOf AnyOf composition edges`` () =
    match readSpecification "Specifications/medium-complex.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectDocumentSchemas doc

        Assert.That(
            graph.Edges |> Seq.exists (fun e -> match e.EdgeKind with Composition AllOf -> true | _ -> false),
            Is.True,
            "Expected AllOf composition edges"
        )

        Assert.That(
            graph.Edges |> Seq.exists (fun e -> match e.EdgeKind with Composition OneOf -> true | _ -> false),
            Is.True,
            "Expected OneOf composition edges"
        )

        Assert.That(
            graph.Edges |> Seq.exists (fun e -> match e.EdgeKind with Composition AnyOf -> true | _ -> false),
            Is.True,
            "Expected AnyOf composition edges"
        )

[<Test>]
let ``medium-complex User allOf edges point to Identifiable and Timestamped`` () =
    match readSpecification "Specifications/medium-complex.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectDocumentSchemas doc

        Assert.That(hasCompositionEdge AllOf "User" "Identifiable" graph, Is.True)
        Assert.That(hasCompositionEdge AllOf "User" "Timestamped" graph, Is.True)

[<Test>]
let ``medium-complex ProductCategory oneOf edges point to CategoryLeaf and CategoryBranch`` () =
    match readSpecification "Specifications/medium-complex.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectDocumentSchemas doc

        Assert.That(hasCompositionEdge OneOf "ProductCategory" "CategoryLeaf" graph, Is.True)
        Assert.That(hasCompositionEdge OneOf "ProductCategory" "CategoryBranch" graph, Is.True)

[<Test>]
let ``medium-complex additionalProperties produces MapValue edges`` () =
    match readSpecification "Specifications/medium-complex.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectDocumentSchemas doc

        Assert.That(hasMapValueEdge "Timestamped" graph, Is.True, "Timestamped.metadata should have MapValue edge")
        Assert.That(hasMapValueEdge "OrderItem" graph, Is.True, "OrderItem.attributes should have MapValue edge")

[<Test>]
let ``Collect schema graph from petstore components`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Ok doc ->
        let g = collectDocumentSchemas doc
        Assert.That(g.Nodes.Count, Is.GreaterThan(0))
        Assert.That(g.Edges.Count, Is.GreaterThanOrEqualTo(0))
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")

[<Test>]
let ``Schema graph node Nullable reflects JsonSchemaType.Null flag`` () =
    let nullableSchema =
        OpenApiSchema(Type = System.Nullable(JsonSchemaType.String ||| JsonSchemaType.Null))
        :> IOpenApiSchema

    let nonNullableSchema =
        OpenApiSchema(Type = System.Nullable JsonSchemaType.String) :> IOpenApiSchema

    let nullableNode =
        collectSchemaGraph nullableSchema
        |> fun g -> g.Nodes |> Seq.exactlyOne

    let nonNullableNode =
        collectSchemaGraph nonNullableSchema
        |> fun g -> g.Nodes |> Seq.exactlyOne

    Assert.That(nullableNode.Nullable, Is.EqualTo(Some true))
    Assert.That(nonNullableNode.Nullable, Is.EqualTo(Some false))



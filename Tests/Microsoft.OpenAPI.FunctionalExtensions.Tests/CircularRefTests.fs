module Microsoft.OpenAPI.FunctionalExtensions.Tests.CircularRefTests

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors
open OpenApiTraversal
open OpenApiOperationsTraversal
open OpenApiSchemaAnalysis

let private circularRefsSpec = "Specifications/circular-refs.yaml"

let private treeNodePointer = Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.referencePointer "TreeNode"
let private mutualAPointer = Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.referencePointer "MutualA"
let private mutualBPointer = Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.referencePointer "MutualB"

let private loadCircularRefs () =
    match readSpecification circularRefsSpec with
    | Ok doc -> doc
    | Error e -> failwith $"Failed to read circular-refs spec: %A{e}"

let private nodesWithId (graph: SchemaGraph) (pointer: string) =
    graph.Nodes |> Seq.filter (fun node -> node.Id = pointer) |> Seq.toList

let private schemaChildren (schema: IOpenApiSchema) : IOpenApiSchema list =
    let properties =
        Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaProperties schema
        |> Map.values
        |> Seq.toList

    let items =
        Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaItems schema
        |> Option.toList

    let additional =
        Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAdditionalProperties schema
        |> Option.toList

    let compositions =
        [ yield! Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAllOf schema
          yield! Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaOneOf schema
          yield! Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAnyOf schema ]

    properties @ items @ additional @ compositions

let private collectChildIdsWithVisited (root: IOpenApiSchema) (rootPointer: string) =
    let visited = System.Collections.Generic.HashSet<string>()
    let ids = System.Collections.Generic.List<string>()

    let rec walk (path: string) (schema: IOpenApiSchema) =
        let id =
            match Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.trySchemaReferenceId schema with
            | Some refId when not (System.String.IsNullOrWhiteSpace refId) ->
                Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.referencePointer refId
            | _ -> path

        if visited.Add id then
            ids.Add id |> ignore
            schemaChildren schema |> List.iter (fun child -> walk $"{path}/child" child)

    walk rootPointer root
    ids |> Seq.toList

[<Test; Timeout(5000)>]
let ``collectDocumentSchemas with circular refs completes within timeout`` () =
    let doc = loadCircularRefs ()
    let graph = collectDocumentSchemas doc
    Assert.That(graph.Nodes.Count, Is.GreaterThan(0))
    Assert.That(graph.Nodes.Count, Is.LessThan(50))

[<Test; Timeout(5000)>]
let ``collectDocumentSchemas yields exactly one TreeNode node for self-reference`` () =
    let doc = loadCircularRefs ()
    let graph = collectDocumentSchemas doc
    let treeNodes = nodesWithId graph treeNodePointer
    Assert.That(treeNodes.Length, Is.EqualTo(1))

[<Test; Timeout(5000)>]
let ``collectRouteMap works with circular-ref schemas`` () =
    let doc = loadCircularRefs ()
    let routeMap = collectRouteMap doc
    Assert.That(routeMap.Routes.Count, Is.EqualTo(1))

    let route = routeMap.Routes |> Seq.exactlyOne
    Assert.That(route.OperationId, Is.EqualTo(Some "listNodes"))
    Assert.That(route.ReturnsArray, Is.True)

[<Test; Timeout(5000)>]
let ``cutDocument with transitive on circular-ref spec completes within timeout`` () =
    let doc = loadCircularRefs ()
    let opts = { ScissorsOptions.Empty with IncludeOperationIds = [ "listNodes" ] }
    let cut = cutDocument doc opts
    Assert.That(cut.Paths.ContainsKey("/nodes"), Is.True)
    Assert.That(cut.Components.Schemas.ContainsKey("TreeNode"), Is.True)

[<Test; Timeout(5000)>]
let ``schemaChildren on self-referencing schema does not infinite-loop`` () =
    let doc = loadCircularRefs ()
    let treeNode = doc.Components.Schemas.["TreeNode"]
    let childIds = collectChildIdsWithVisited treeNode treeNodePointer
    Assert.That(childIds.Length, Is.GreaterThan(0))
    Assert.That(childIds |> List.filter ((=) treeNodePointer) |> List.length, Is.EqualTo(1))

[<Test; Timeout(5000)>]
let ``MutualA and MutualB produce exactly two nodes with cross edges`` () =
    let doc = loadCircularRefs ()
    let mutualA = doc.Components.Schemas.["MutualA"]
    let graph = collectSchemaGraphWithRoot mutualA mutualAPointer

    let mutualNodes =
        graph.Nodes
        |> Seq.filter (fun node -> node.Id = mutualAPointer || node.Id = mutualBPointer)
        |> Seq.toList

    Assert.That(mutualNodes.Length, Is.EqualTo(2))

    let hasEdge fromId toId =
        graph.Edges |> Seq.exists (fun edge -> edge.FromId = fromId && edge.ToId = toId)

    Assert.That(hasEdge mutualAPointer mutualBPointer, Is.True)
    Assert.That(hasEdge mutualBPointer mutualAPointer, Is.True)

module Microsoft.OpenAPI.FunctionalExtensions.Tests.SnapshotTests

open System.Text.Json
open VerifyNUnit
open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiTraversal
open OpenApiOperationsTraversal
open OpenApiSchemaAnalysis
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns
open ResultEx

let private jsonOptions = JsonSerializerOptions(WriteIndented = true)

let private serialize obj = JsonSerializer.Serialize(obj, jsonOptions)

let private requireDoc path : OpenApiDocument =
    match readSpecification path with
    | Ok doc -> doc
    | Error e -> raise (System.InvalidOperationException $"Failed to read spec: %A{e}")

type SchemaNodeSnapshot = {
    Id: string
    Title: string option
    Kind: string option
    Nullable: bool option
    Description: string option
    Format: string option
    ReadOnly: bool option
    EnumValues: string list option
    WriteOnly: bool option
}

type SchemaEdgeSnapshot = {
    FromId: string
    ToId: string
    EdgeKind: string
}

type SchemaGraphSnapshot = {
    Nodes: SchemaNodeSnapshot list
    Edges: SchemaEdgeSnapshot list
}

let private toNodeSnapshot (node: SchemaNode) =
    { Id = node.Id
      Title = node.Title
      Kind = node.Kind |> Option.map string
      Nullable = node.Nullable
      Description = node.Description
      Format = node.Format
      ReadOnly = node.ReadOnly
      EnumValues = node.EnumValues
      WriteOnly = node.WriteOnly }

let private edgeKindToString = function
    | Property name -> $"Property({name})"
    | ArrayItem -> "ArrayItem"
    | MapValue -> "MapValue"
    | Composition AllOf -> "Composition(AllOf)"
    | Composition OneOf -> "Composition(OneOf)"
    | Composition AnyOf -> "Composition(AnyOf)"

let private toGraphSnapshot (graph: SchemaGraph) =
    let nodes =
        graph.Nodes
        |> Seq.sortBy (fun n -> n.Id)
        |> Seq.map toNodeSnapshot
        |> Seq.toList

    let edges =
        graph.Edges
        |> Seq.map (fun e ->
            { FromId = e.FromId
              ToId = e.ToId
              EdgeKind = edgeKindToString e.EdgeKind })
        |> Seq.sortBy (fun e -> e.FromId, e.ToId, e.EdgeKind)
        |> Seq.toList

    { Nodes = nodes; Edges = edges }

type ClassificationSnapshot = {
    Name: string
    Kind: string
    ElementRef: string option
    ReferenceId: string option
    PrimitiveKind: string option
    Format: string option
    Nullable: bool option
}

let private toClassificationSnapshot name (schema: IOpenApiSchema) =
    match classifySchema schema with
    | Array el ->
        { Name = name
          Kind = "Array"
          ElementRef = el |> Option.bind tryGetReferenceId
          ReferenceId = None
          PrimitiveKind = None
          Format = None
          Nullable = None }
    | Reference id ->
        { Name = name
          Kind = "Reference"
          ElementRef = None
          ReferenceId = Some id
          PrimitiveKind = None
          Format = None
          Nullable = None }
    | Primitive (kind, format, nullable) ->
        { Name = name
          Kind = "Primitive"
          ElementRef = None
          ReferenceId = None
          PrimitiveKind = Some kind
          Format = format
          Nullable = Some nullable }
    | Object ->
        { Name = name
          Kind = "Object"
          ElementRef = None
          ReferenceId = None
          PrimitiveKind = None
          Format = None
          Nullable = None }

let private classifyComponentSchemas (doc: OpenApiDocument) =
    if isNull doc.Components || isNull doc.Components.Schemas then
        []
    else
        doc.Components.Schemas
        |> Seq.map (fun kv -> toClassificationSnapshot kv.Key kv.Value)
        |> Seq.sortBy (fun x -> x.Name)
        |> Seq.toList

[<Test>]
let ``Schema graph IR snapshot`` () = task {
    let doc = requireDoc "Specifications/petstore.yaml"
    let graph = collectDocumentSchemas doc |> toGraphSnapshot
    let json = serialize graph
    let! _ = Verifier.Verify(json)
    return ()
}

[<Test>]
let ``Route map IR snapshot`` () = task {
    let doc = requireDoc "Specifications/petstore.yaml"
    let routeMap = collectRouteMap doc
    let json = serialize routeMap
    let! _ = Verifier.Verify(json)
    return ()
}

[<Test>]
let ``Schema classification snapshot`` () = task {
    let doc = requireDoc "Specifications/petstore.yaml"
    let classifications = classifyComponentSchemas doc
    let json = serialize classifications
    let! _ = Verifier.Verify(json)
    return ()
}

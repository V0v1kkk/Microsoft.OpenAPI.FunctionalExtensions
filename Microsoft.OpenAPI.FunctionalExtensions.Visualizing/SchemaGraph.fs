module Microsoft.OpenAPI.FunctionalExtensions.Visualizing.SchemaGraph

open System
open System.Collections.Generic
open Microsoft.OpenApi
open OpenApiSchemaAnalysis
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiAdapters

type CompositionKind =
  | AllOf
  | OneOf
  | AnyOf

type SchemaEdgeKind =
  | Property of propertyName:string
  | ArrayItem
  | MapValue
  | Composition of CompositionKind

type SchemaNodeRef = string

type SchemaNode = {
  Id: SchemaNodeRef
  Title: string option
  Kind: string option
  Nullable: bool option
  Description: string option
  Format: string option
  ReadOnly: bool option
  EnumValues: string list option
  WriteOnly: bool option
}

type SchemaEdge = {
  FromId: SchemaNodeRef
  ToId: SchemaNodeRef
  EdgeKind: SchemaEdgeKind
}

type SchemaGraph = {
  Nodes: ResizeArray<SchemaNode>
  Edges: ResizeArray<SchemaEdge>
}

let private nodeId (rootPointer: string) (schema: IOpenApiSchema) : SchemaNodeRef =
  match trySchemaRefName schema with
  | Some id when not (String.IsNullOrWhiteSpace id) -> $"#/components/schemas/{id}"
  | None when not (String.IsNullOrWhiteSpace schema.Id) -> $"#/components/schemas/{schema.Id}"
  | _ -> rootPointer

let private addNode (graph: SchemaGraph) (id: SchemaNodeRef) (schema: IOpenApiSchema) =
  graph.Nodes.Add {
    Id = id
    Title = if System.String.IsNullOrWhiteSpace schema.Title then None else Some schema.Title
    Kind = schema.Type |> Option.ofNullable |> Option.map string
    Nullable = None
    Description = schema.Description |> Option.ofObj
    Format = schema.Format |> Option.ofObj
    ReadOnly = Some schema.ReadOnly
    EnumValues =
      if isNull schema.Enum || schema.Enum.Count = 0 then None
      else
        Some (
          schema.Enum
          |> Seq.choose (fun n -> if isNull n then None else Some (n.ToJsonString()))
          |> Seq.toList)
    WriteOnly = Some schema.WriteOnly
  }

let private addEdge (graph: SchemaGraph) fromId toId kind =
  graph.Edges.Add { FromId = fromId; ToId = toId; EdgeKind = kind }

let collectSchemaGraphWithRoot (root: IOpenApiSchema) (rootPointer: string) : SchemaGraph =
  let graph = { Nodes = ResizeArray(); Edges = ResizeArray() }
  let visited = HashSet<SchemaNodeRef>()

  let rec walk (path: string) (schema: IOpenApiSchema) (edgeFrom: SchemaNodeRef option) (edgeKind: SchemaEdgeKind option) =
    let id = nodeId path schema
    if visited.Add id then addNode graph id schema
    match edgeFrom, edgeKind with
    | Some f, Some k -> addEdge graph f id k
    | _ -> ()

    if schema.Properties <> null then
      for kv in schema.Properties do
        walk ($"{path}/properties/{kv.Key}") kv.Value (Some id) (Some (Property kv.Key))

    if schema.Items <> null then
      // when array of a referenced component, use the component pointer for the child, not '/items'
      let childPath =
        match trySchemaRefName schema.Items with
        | Some rid when not (String.IsNullOrWhiteSpace rid) -> $"#/components/schemas/{rid}"
        | _ -> $"{path}/items"
      walk childPath schema.Items (Some id) (Some ArrayItem)

    if schema.AdditionalProperties <> null then
      walk ($"{path}/additionalProperties") schema.AdditionalProperties (Some id) (Some MapValue)

    let inline each (kind: CompositionKind) (schemas: System.Collections.Generic.IList<IOpenApiSchema>) (seg: string) =
      if not (isNull schemas) then
        for s in schemas do
          walk ($"{path}/{seg}") s (Some id) (Some (Composition kind))

    each AllOf schema.AllOf "allOf"
    each OneOf schema.OneOf "oneOf"
    each AnyOf schema.AnyOf "anyOf"

  walk rootPointer root None None
  graph

let collectSchemaGraph (root: IOpenApiSchema) : SchemaGraph =
  collectSchemaGraphWithRoot root "#"

let collectDocumentSchemas (doc: OpenApiDocument) : SchemaGraph =
  let graph = { Nodes = ResizeArray(); Edges = ResizeArray() }
  if doc.Components <> null && doc.Components.Schemas <> null then
    for kv in doc.Components.Schemas do
      let sub = collectSchemaGraphWithRoot kv.Value ($"#/components/schemas/{kv.Key}")
      sub.Nodes |> Seq.iter graph.Nodes.Add
      sub.Edges |> Seq.iter graph.Edges.Add
  graph



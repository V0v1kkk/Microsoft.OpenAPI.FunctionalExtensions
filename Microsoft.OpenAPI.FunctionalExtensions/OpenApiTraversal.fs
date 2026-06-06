module OpenApiTraversal

open System
open System.Collections.Generic
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns

type CompositionKind =
  | AllOf
  | OneOf
  | AnyOf

type SchemaEdgeKind =
  | Property of propertyName:string
  | ArrayItem
  | MapValue
  | Composition of CompositionKind

type SchemaNodeRef = string // JSON pointer (or reference Id if available)

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
  // Prefer component pointer when schema is a reference or named component; fall back to traversal path
  match Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.trySchemaReferenceId schema with
  | Some id when not (String.IsNullOrWhiteSpace id) ->
      Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.referencePointer id
  | _ -> rootPointer

let private addNode (graph: SchemaGraph) (id: SchemaNodeRef) (schema: IOpenApiSchema) =
  let enumValues =
    match Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaEnum schema with
    | [] -> None
    | nodes ->
        Some (nodes |> List.map (fun node -> node.ToJsonString()))

  graph.Nodes.Add {
    Id = id
    Title = Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaTitle schema
    Kind =
      Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaType schema
      |> Option.map string
    Nullable = None // v2 uses JsonSchemaType flags including Null; compute later in label
    Description = Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaDescription schema
    Format = Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaFormat schema
    ReadOnly = Some (Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaReadOnly schema)
    EnumValues = enumValues
    WriteOnly = Some (Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaWriteOnly schema)
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

    Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaProperties schema
    |> Map.iter (fun key value ->
        walk ($"{path}/properties/{key}") value (Some id) (Some (Property key)))

    // items — when array of a referenced component, use the component pointer for the child, not '/items'
    match schema with
    | ArraySchema items ->
        let childPath =
          match Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.trySchemaReferenceId items with
          | Some rid when not (String.IsNullOrWhiteSpace rid) ->
              Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.referencePointer rid
          | _ -> $"{path}/items"
        walk childPath items (Some id) (Some ArrayItem)
    | _ -> ()

    match Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAdditionalProperties schema with
    | Some additionalProperties ->
        walk ($"{path}/additionalProperties") additionalProperties (Some id) (Some MapValue)
    | None -> ()

    let inline each (kind: CompositionKind) (schemas: IOpenApiSchema list) (seg: string) =
      for s in schemas do
        walk ($"{path}/{seg}") s (Some id) (Some (Composition kind))

    each AllOf (Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAllOf schema) "allOf"
    each OneOf (Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaOneOf schema) "oneOf"
    each AnyOf (Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAnyOf schema) "anyOf"

  walk rootPointer root None None
  graph

let collectSchemaGraph (root: IOpenApiSchema) : SchemaGraph =
  collectSchemaGraphWithRoot root "#"

let collectDocumentSchemas (doc: OpenApiDocument) : SchemaGraph =
  let graph = { Nodes = ResizeArray(); Edges = ResizeArray() }

  Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.documentSchemas doc
  |> Map.iter (fun key schema ->
      let sub = collectSchemaGraphWithRoot schema ($"#/components/schemas/{key}")
      sub.Nodes |> Seq.iter graph.Nodes.Add
      sub.Edges |> Seq.iter graph.Edges.Add)

  graph

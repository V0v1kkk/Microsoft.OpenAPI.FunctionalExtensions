[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters

open System.Collections.Generic
open System.Net.Http
open Microsoft.OpenApi

let private readComponentsDictionary
    (document: OpenApiDocument)
    (selector: OpenApiComponents -> IDictionary<string, 'v>)
    : Map<string, 'v> =
  match document with
  | null -> Map.empty
  | doc ->
      match AdapterCore.ofObj doc.Components with
      | None -> Map.empty
      | Some components ->
          match selector components with
          | null -> Map.empty
          | dictionary -> AdapterCore.readMap dictionary

let documentComponents (document: OpenApiDocument) : OpenApiComponents option =
  match document with
  | null -> None
  | doc -> AdapterCore.ofObj doc.Components

let documentSchemas (document: OpenApiDocument) : Map<string, IOpenApiSchema> =
  readComponentsDictionary document (fun components -> components.Schemas)

let documentResponses (document: OpenApiDocument) : Map<string, IOpenApiResponse> =
  readComponentsDictionary document (fun components -> components.Responses)

let documentParameters (document: OpenApiDocument) : Map<string, IOpenApiParameter> =
  readComponentsDictionary document (fun components -> components.Parameters)

let documentRequestBodies (document: OpenApiDocument) : Map<string, IOpenApiRequestBody> =
  readComponentsDictionary document (fun components -> components.RequestBodies)

let documentHeaders (document: OpenApiDocument) : Map<string, IOpenApiHeader> =
  readComponentsDictionary document (fun components -> components.Headers)

let documentServers (document: OpenApiDocument) : OpenApiServer list =
  match document with
  | null -> []
  | doc -> AdapterCore.readSeq doc.Servers

let documentTags (document: OpenApiDocument) : string list =
  match document with
  | null -> []
  | doc ->
      match doc.Tags with
      | null -> []
      | tags ->
          tags
          |> Seq.choose (fun tag ->
              match tag with
              | null -> None
              | t -> AdapterCore.ofObj t.Name)
          |> Seq.toList

let documentPaths (document: OpenApiDocument) : (string * IOpenApiPathItem) list =
  match document with
  | null -> []
  | doc ->
      match doc.Paths with
      | null -> []
      | paths -> paths |> Seq.map (fun entry -> entry.Key, entry.Value) |> Seq.toList

let foldAllOperations
    (document: OpenApiDocument)
    (folder: 's -> string * HttpMethod * OpenApiOperation -> 's)
    (state: 's)
    : 's =
  documentPaths document
  |> List.fold (fun accumulatedState (path, pathItem) ->
      OperationAdapters.pathItemOperations pathItem
      |> List.fold (fun innerState (method, operation) ->
          folder innerState (path, method, operation)) accumulatedState) state

let allOperations (document: OpenApiDocument) : (string * HttpMethod * OpenApiOperation) list =
  foldAllOperations document (fun accumulated operation -> operation :: accumulated) []
  |> List.rev

let tryComponentSchema (document: OpenApiDocument) (name: string) : IOpenApiSchema option =
  documentSchemas document |> Map.tryFind name


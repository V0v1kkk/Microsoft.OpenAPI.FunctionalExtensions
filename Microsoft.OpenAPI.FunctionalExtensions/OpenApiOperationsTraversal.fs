module OpenApiOperationsTraversal

open System
open System.Collections.Generic
open System.Net.Http
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiAdapters
open SeqExtensions

type Route = {
  Path: string
  Method: string
  OperationId: string option
  Tags: string list
  ParameterSchemas: string list
  RequestSchemas: string list
  ResponseSchemas: string list
  ReturnsArray: bool
  ReturnsArrayViaData: bool
  HasOperations: bool
}

type RouteMap = {
  Routes: ResizeArray<Route>
}

let private toMethodString (m: HttpMethod) = m.Method

let private schemaRefPointerFromInterface (schema: IOpenApiSchema) : string option =
  schema
  |> trySchemaRefName
  |> Option.map (fun id -> $"#/components/schemas/{id}")
  |> Option.orElseWith (fun () ->
      match schema with
      | :? OpenApiSchema as s when not (String.IsNullOrWhiteSpace s.Id) -> Some ($"#/components/schemas/{s.Id}")
      | _ -> None)

let private collectSchemasFromContent (content: IDictionary<string, OpenApiMediaType>) : seq<string> =
  schemasFromContent content
  |> Seq.choose schemaRefPointerFromInterface

let private collectParameterSchemas (parameters: IList<IOpenApiParameter>) : seq<string> =
  match parameters with
  | null -> Seq.empty
  | ps ->
      ps
      |> Seq.collect (fun p ->
          match p with
          | null -> Seq.empty
          | _ when not (isNull p.Schema) ->
              match schemaRefPointerFromInterface p.Schema with
              | Some x -> seq { yield x }
              | None -> Seq.empty
          | _ when not (isNull p.Content) -> collectSchemasFromContent p.Content
          | _ -> Seq.empty)

let private collectRequestSchemas (request: IOpenApiRequestBody) : seq<string> =
  match request with
  | null -> Seq.empty
  | r -> collectSchemasFromContent r.Content

let private collectResponseSchemas (responses: OpenApiResponses) : seq<string> =
  match responses with
  | null -> Seq.empty
  | rs -> rs |> Seq.collect (fun kv -> match kv.Value with null -> Seq.empty | r -> collectSchemasFromContent r.Content)

let private collectResponseSchemaObjects (responses: OpenApiResponses) : seq<OpenApiSchema> =
  match responses with
  | null -> Seq.empty
  | rs ->
      rs
      |> Seq.collect (fun kv ->
          match kv.Value with
          | null -> Seq.empty
          | r when isNull r.Content -> Seq.empty
          | r ->
              r.Content.Values
              |> Seq.choose (fun mt ->
                  match mt with
                  | null -> None
                  | m when isNull m.Schema -> None
                  | m ->
                      match m.Schema with
                      | :? OpenApiSchema as s -> Some s
                      | _ -> None))

let collectRouteMap (doc: OpenApiDocument) : RouteMap =
  let routes = ResizeArray<Route>()

  let rec resolveSchema (s: OpenApiSchema) : OpenApiSchema =
    if isNull s then null
    elif not (String.IsNullOrWhiteSpace s.Id) && not (isNull doc.Components) && not (isNull doc.Components.Schemas) then
      match doc.Components.Schemas.TryGetValue s.Id with
      | true, sch -> (sch :?> OpenApiSchema)
      | _ -> s
    else s

  let rec isArraySchema (s: OpenApiSchema) : bool =
    match resolveSchema s with
    | null -> false
    | s' when s'.Type.HasValue && (s'.Type.Value &&& JsonSchemaType.Array) = JsonSchemaType.Array -> true
    | s' when not (isNull s'.Items) -> true
    | _ -> false

  let rec isViaDataArray (s: OpenApiSchema) : bool =
    match resolveSchema s with
    | null -> false
    | s' when isNull s'.Properties -> false
    | s' ->
        match s'.Properties.TryGetValue "data" with
        | true, dataSch -> isArraySchema (dataSch :?> OpenApiSchema)
        | _ -> false

  // Use adapter to fold operations in a functional way
  let folder (state: unit) (path: string, opType: HttpMethod, op: OpenApiOperation) =
    let parameterSchemas = collectParameterSchemas op.Parameters |> Seq.distinct |> Seq.toList
    let requestSchemas = collectRequestSchemas op.RequestBody |> Seq.distinct |> Seq.toList
    let responseSchemasSeq = collectResponseSchemas op.Responses
    let responseSchemas = responseSchemasSeq |> Seq.distinct |> Seq.toList
    let returnsArray =
      collectResponseSchemaObjects op.Responses
      |> Seq.exists (fun sch -> let s = resolveSchema sch in isArraySchema s)
    let returnsArrayViaData =
      collectResponseSchemaObjects op.Responses
      |> Seq.exists (fun sch -> let s = resolveSchema sch in isViaDataArray s)
    let route = {
      Path = path
      Method = toMethodString opType
      OperationId = if String.IsNullOrWhiteSpace op.OperationId then None else Some op.OperationId
      Tags = if isNull op.Tags then [] else op.Tags |> Seq.choose (fun t -> if isNull t then None else Some t.Name) |> Seq.toList
      ParameterSchemas = parameterSchemas
      RequestSchemas = requestSchemas
      ResponseSchemas = responseSchemas
      ReturnsArray = returnsArray
      ReturnsArrayViaData = returnsArrayViaData
      HasOperations = true
    }
    routes.Add route
    state

  foldOperations doc folder () |> ignore
  { Routes = routes }



module OpenApiOperationsTraversal

open System
open System.Net.Http
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns

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

let private schemaRefPointer (schema: IOpenApiSchema) : string option =
  Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.trySchemaReferenceId schema
  |> Option.map Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.referencePointer

let private schemaRefPointers (schemas: seq<IOpenApiSchema>) : string list =
  schemas |> Seq.choose schemaRefPointer |> Seq.distinct |> Seq.toList

let private parameterSchemaRefs (parameters: IOpenApiParameter list) : string list =
  parameters
  |> List.collect (fun parameter ->
      match parameter with
      | null -> []
      | param ->
          match param.Schema with
          | null ->
              Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.schemasFromContent param.Content
              |> List.toSeq
              |> schemaRefPointers
          | schema -> schemaRefPointer schema |> Option.toList)
  |> List.distinct

let private requestSchemaRefs (request: IOpenApiRequestBody option) : string list =
  request
  |> Option.map (fun body ->
      Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.schemasFromContent body.Content
      |> List.toSeq
      |> schemaRefPointers)
  |> Option.defaultValue []

let private responseSchemaRefs (responses: Map<string, IOpenApiResponse>) : string list =
  responses
  |> Map.values
  |> Seq.collect (fun response ->
      match response with
      | null -> Seq.empty
      | resp ->
          Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.schemasFromContent resp.Content
          |> List.toSeq)
  |> schemaRefPointers

let private responseSchemaObjects (responses: Map<string, IOpenApiResponse>) : IOpenApiSchema list =
  responses
  |> Map.values
  |> Seq.collect (fun response ->
      match response with
      | null -> Seq.empty
      | resp ->
          Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.schemasFromContent resp.Content
          |> List.toSeq
          |> Seq.choose (function
              | :? OpenApiSchema as schema -> Some (schema :> IOpenApiSchema)
              | _ -> None))
  |> Seq.toList

let private resolveSchema (document: OpenApiDocument) (schema: IOpenApiSchema) : IOpenApiSchema option =
  match Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.tryResolveSchemaReference document schema with
  | Some resolved -> Some resolved
  | None ->
      match schema with
      | null -> None
      | unresolved -> Some unresolved

let private isArraySchema (document: OpenApiDocument) (schema: IOpenApiSchema) : bool =
  match resolveSchema document schema with
  | None -> false
  | Some resolved ->
      let hasArrayType =
        Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaType resolved
        |> Option.exists (fun schemaType -> (schemaType &&& JsonSchemaType.Array) = JsonSchemaType.Array)

      let hasItems =
        Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaItems resolved
        |> Option.isSome

      hasArrayType || hasItems

let private isViaDataArray (document: OpenApiDocument) (schema: IOpenApiSchema) : bool =
  match resolveSchema document schema with
  | None -> false
  | Some resolved ->
      match resolved with
      | ObjectSchema properties ->
          match properties.TryGetValue "data" with
          | true, dataSchema -> isArraySchema document dataSchema
          | _ -> false
      | _ -> false

let collectRouteMap (doc: OpenApiDocument) : RouteMap =
  let routes = ResizeArray<Route>()

  let folder (state: unit) (path: string, method: HttpMethod, operation: OpenApiOperation) =
    let responses = Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.operationResponses operation
    let responseObjects = responseSchemaObjects responses

    let route = {
      Path = path
      Method = toMethodString method
      OperationId =
        if String.IsNullOrWhiteSpace operation.OperationId then None
        else Some operation.OperationId
      Tags = Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.operationTags operation
      ParameterSchemas =
        Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.operationParameters operation
        |> parameterSchemaRefs
      RequestSchemas =
        Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.operationRequestBody operation
        |> requestSchemaRefs
      ResponseSchemas = responseSchemaRefs responses
      ReturnsArray = responseObjects |> List.exists (isArraySchema doc)
      ReturnsArrayViaData = responseObjects |> List.exists (isViaDataArray doc)
      HasOperations = true
    }

    routes.Add route
    state

  Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.foldAllOperations doc folder () |> ignore
  { Routes = routes }

[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters

open System.Collections.Generic
open System.Net.Http
open Microsoft.OpenApi

let pathItemOperations (pathItem: IOpenApiPathItem) : (HttpMethod * OpenApiOperation) list =
  match pathItem with
  | null -> []
  | item ->
      match item.Operations with
      | null -> []
      | operations ->
          operations
          |> Seq.map (fun entry -> entry.Key, entry.Value)
          |> Seq.toList

let operationTags (operation: OpenApiOperation) : string list =
  match operation with
  | null -> []
  | op ->
      match op.Tags with
      | null -> []
      | tags ->
          tags
          |> Seq.choose (fun tag ->
              match tag with
              | null -> None
              | t -> AdapterCore.ofObj t.Name)
          |> Seq.toList

let operationParameters (operation: OpenApiOperation) : IOpenApiParameter list =
  match operation with
  | null -> []
  | op -> AdapterCore.readSeq op.Parameters

let operationRequestBody (operation: OpenApiOperation) : IOpenApiRequestBody option =
  match operation with
  | null -> None
  | op -> AdapterCore.ofObj op.RequestBody

let operationResponses (operation: OpenApiOperation) : Map<string, IOpenApiResponse> =
  match operation with
  | null -> Map.empty
  | op -> AdapterCore.readMap op.Responses

let mediaTypeSchema (mediaType: IOpenApiMediaType) : IOpenApiSchema option =
  match mediaType with
  | null -> None
  | mt -> AdapterCore.ofObj mt.Schema

let schemasFromContent (content: IDictionary<string, IOpenApiMediaType>) : IOpenApiSchema list =
  match content with
  | null -> []
  | dictionary ->
      dictionary.Values
      |> AdapterCore.readSeq
      |> List.choose mediaTypeSchema

let private schemasFromParameter (parameter: IOpenApiParameter) : IOpenApiSchema list =
  match parameter with
  | null -> []
  | param ->
      match AdapterCore.ofObj param.Schema with
      | Some schema -> [ schema ]
      | None -> schemasFromContent param.Content

let schemasFromOperation (operation: OpenApiOperation) : IOpenApiSchema list =
  match operation with
  | null -> []
  | op ->
      [
        yield! operationParameters op |> List.collect schemasFromParameter
        yield!
          match operationRequestBody op with
          | None -> []
          | Some requestBody -> schemasFromContent requestBody.Content
        yield!
          operationResponses op
          |> Map.values
          |> Seq.collect (fun response ->
              match response with
              | null -> []
              | resp -> schemasFromContent resp.Content)
          |> Seq.toList
      ]

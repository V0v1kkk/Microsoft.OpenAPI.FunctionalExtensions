module OpenApiLinksTraversal

open System
open System.Net.Http
open Microsoft.OpenApi

type LinkSource =
  | ResponseBody of jsonPointer: string
  | ResponseHeader of headerName: string
  | RequestBody of jsonPointer: string

type LinkTarget =
  | OperationParameter of parameterName: string
  | RequestBodyField of jsonPointer: string

type OperationLink = {
  LinkName: string
  SourceOperationId: string
  TargetOperationId: string
  Source: LinkSource
  Target: LinkTarget
  Description: string option
}

type LinksGraph = {
  Operations: string list
  Links: OperationLink list
}

let private responseBodyPrefix = "$response.body#/"
let private responseHeaderPrefix = "$response.header."
let private requestBodyPrefix = "$request.body#/"

let private runtimeExpressionToString (wrapper: RuntimeExpressionAnyWrapper) : string option =
  match wrapper with
  | null -> None
  | value ->
      match Microsoft.OpenAPI.FunctionalExtensions.AdapterCore.ofObj value.Expression with
      | Some expression -> Some expression.Expression
      | None ->
          match Microsoft.OpenAPI.FunctionalExtensions.AdapterCore.ofObj value.Any with
          | Some node -> Some (node.ToJsonString())
          | None -> None

let private parseLinkSource (expression: string) : LinkSource option =
  match expression with
  | null | "" -> None
  | expr when expr.StartsWith(responseBodyPrefix, StringComparison.Ordinal) ->
      Some (ResponseBody (expr.Substring(responseBodyPrefix.Length)))
  | expr when expr.StartsWith(responseHeaderPrefix, StringComparison.Ordinal) ->
      Some (ResponseHeader (expr.Substring(responseHeaderPrefix.Length)))
  | expr when expr.StartsWith(requestBodyPrefix, StringComparison.Ordinal) ->
      Some (RequestBody (expr.Substring(requestBodyPrefix.Length)))
  | _ -> None

let private tryOperationId (operation: OpenApiOperation) : string option =
  match operation with
  | null -> None
  | op ->
      if String.IsNullOrWhiteSpace op.OperationId then None
      else Some op.OperationId

let private tryTargetOperationId (link: IOpenApiLink) : string option =
  match link with
  | null -> None
  | l ->
      if not (String.IsNullOrWhiteSpace l.OperationId) then
        Some l.OperationId
      else
        None

let private tryLinkDescription (link: IOpenApiLink) : string option =
  match link with
  | :? OpenApiLink as concrete when not (String.IsNullOrWhiteSpace concrete.Description) ->
      Some concrete.Description
  | :? OpenApiLinkReference as reference when not (String.IsNullOrWhiteSpace reference.Description) ->
      Some reference.Description
  | _ -> None

let private linksFromParameters
    (linkName: string)
    (sourceOperationId: string)
    (targetOperationId: string)
    (description: string option)
    (parameters: Map<string, RuntimeExpressionAnyWrapper>)
    : OperationLink list =
  parameters
  |> Map.toList
  |> List.choose (fun (parameterName, wrapper) ->
      match runtimeExpressionToString wrapper with
      | None -> None
      | Some expression ->
      match parseLinkSource expression with
      | None -> None
      | Some source ->
          Some {
            LinkName = linkName
            SourceOperationId = sourceOperationId
            TargetOperationId = targetOperationId
            Source = source
            Target = OperationParameter parameterName
            Description = description
          })

let private linkFromRequestBody
    (linkName: string)
    (sourceOperationId: string)
    (targetOperationId: string)
    (description: string option)
    (requestBody: RuntimeExpressionAnyWrapper)
    : OperationLink option =
  match runtimeExpressionToString requestBody with
  | None -> None
  | Some requestBodyExpression ->
  match parseLinkSource requestBodyExpression with
  | None -> None
  | Some source ->
      Some {
        LinkName = linkName
        SourceOperationId = sourceOperationId
        TargetOperationId = targetOperationId
        Source = source
        Target = RequestBodyField "/"
        Description = description
      }

let private linksFromResponse
    (sourceOperationId: string)
    (response: IOpenApiResponse)
    : OperationLink list =
  match response with
  | null -> []
  | resp ->
      Microsoft.OpenAPI.FunctionalExtensions.AdapterCore.readMap resp.Links
      |> Map.toList
      |> List.collect (fun (linkName, link) ->
          match tryTargetOperationId link with
          | None -> []
          | Some targetOperationId ->
              let description = tryLinkDescription link
              let parameterLinks =
                match link.Parameters with
                | null -> []
                | parameters ->
                    Microsoft.OpenAPI.FunctionalExtensions.AdapterCore.readMap parameters
                    |> linksFromParameters linkName sourceOperationId targetOperationId description

              let requestBodyLink =
                match link.RequestBody with
                | null -> None
                | requestBody -> linkFromRequestBody linkName sourceOperationId targetOperationId description requestBody

              match requestBodyLink with
              | None -> parameterLinks
              | Some bodyLink -> bodyLink :: parameterLinks)

let private linksFromOperation (sourceOperationId: string) (operation: OpenApiOperation) : OperationLink list =
  Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.operationResponses operation
  |> Map.values
  |> Seq.collect (linksFromResponse sourceOperationId)
  |> Seq.toList

let private distinctOperations (links: OperationLink list) : string list =
  links
  |> List.collect (fun link -> [ link.SourceOperationId; link.TargetOperationId ])
  |> List.distinct
  |> List.sort

let collectLinksGraph (document: OpenApiDocument) : LinksGraph =
  let links =
    Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.foldAllOperations document (fun accumulated (_, _, operation) ->
        match tryOperationId operation with
        | None -> accumulated
        | Some sourceOperationId ->
            linksFromOperation sourceOperationId operation @ accumulated) []

  {
    Operations = distinctOperations links
    Links = List.rev links
  }

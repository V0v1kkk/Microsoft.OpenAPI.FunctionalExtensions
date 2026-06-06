module Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors

open System
open System.Collections.Generic
open System.Net.Http
open Microsoft.OpenApi

type ScissorsOptions = {
  IncludeTags: string list
  IncludePaths: string list
  IncludeOperationIds: string list
  Transitive: bool
}
with
  static member Empty = { IncludeTags = []; IncludePaths = []; IncludeOperationIds = []; Transitive = true }

let private anyFilterSpecified (opts: ScissorsOptions) =
  match opts.IncludeTags, opts.IncludePaths, opts.IncludeOperationIds with
  | [], [], [] -> false
  | _ -> true

let private pathMatches (opts: ScissorsOptions) (path: string) =
  opts.IncludePaths |> List.exists (fun p -> path.Contains(p, StringComparison.Ordinal))

let private operationMatches (opts: ScissorsOptions) (path: string) (_methodKey: HttpMethod) (op: OpenApiOperation) =
  let byPath =
    match opts.IncludePaths with
    | [] -> false
    | _ -> pathMatches opts path
  let byOpId =
    match opts.IncludeOperationIds, op with
    | [], _ -> false
    | _, null -> false
    | _, _ when String.IsNullOrWhiteSpace op.OperationId -> false
    | ids, _ -> ids |> List.exists (fun id -> String.Equals(id, op.OperationId, StringComparison.Ordinal))
  let byTag =
    match opts.IncludeTags, op with
    | [], _ -> false
    | _, null -> false
    | tags, _ ->
        Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.operationTags op
        |> List.exists (fun tagName -> tags |> List.exists (fun it -> String.Equals(it, tagName, StringComparison.OrdinalIgnoreCase)))
  match anyFilterSpecified opts with
  | false -> true
  | true -> byPath || byOpId || byTag

let private tryParseComponentNameFromRef (referenceString: string) : string option =
  if String.IsNullOrWhiteSpace referenceString then None
  else
    let lastSlash = referenceString.LastIndexOf('/')
    if lastSlash >= 0 && lastSlash < referenceString.Length - 1 then
      Some (referenceString.Substring(lastSlash + 1))
    else
      Some (referenceString.TrimStart('#', '/'))

let private tryReferenceId (schema: IOpenApiSchema) : string option =
  match schema with
  | :? OpenApiSchemaReference as reference ->
      match reference.Reference with
      | null -> None
      | ref ->
          if not (String.IsNullOrWhiteSpace ref.Id) then tryParseComponentNameFromRef ref.Id
          else tryParseComponentNameFromRef ref.ReferenceV3
  | _ -> Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.trySchemaReferenceId schema

let private visitKey (schema: IOpenApiSchema) : string =
  match tryReferenceId schema with
  | Some id -> Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.referencePointer id
  | None -> $"schema:{hash schema}"

let rec private collectSchemaRefsFromSchema
    (doc: OpenApiDocument)
    (visited: Set<string>)
    (acc: HashSet<string>)
    (schema: IOpenApiSchema)
    : Set<string> =
  match schema with
  | null -> visited
  | s ->
      let key = visitKey s
      if Set.contains key visited then visited
      else
        let visited = Set.add key visited

        let visited =
          match tryReferenceId s with
          | None -> visited
          | Some name ->
              acc.Add name |> ignore
              match Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.tryComponentSchema doc name with
              | None -> visited
              | Some sub -> collectSchemaRefsFromSchema doc visited acc sub

        match tryReferenceId s with
        | Some _ -> visited
        | None ->
            let walkChild (child: IOpenApiSchema) =
              collectSchemaRefsFromSchema doc visited acc child |> ignore

            Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaProperties s
            |> Map.iter (fun _ value -> walkChild value)

            Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaItems s
            |> Option.iter walkChild

            Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAdditionalProperties s
            |> Option.iter walkChild

            Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAllOf s
            |> List.iter walkChild

            Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaOneOf s
            |> List.iter walkChild

            Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAnyOf s
            |> List.iter walkChild
            visited

let private collectSchemasFromContent (doc: OpenApiDocument) (acc: HashSet<string>) (content: IDictionary<string, IOpenApiMediaType>) =
  for schema in Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.schemasFromContent content do
    collectSchemaRefsFromSchema doc Set.empty acc schema |> ignore

let private collectSchemaRefsFromOperation (doc: OpenApiDocument) (acc: HashSet<string>) (op: OpenApiOperation) =
  for schema in Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.schemasFromOperation op do
    collectSchemaRefsFromSchema doc Set.empty acc schema |> ignore

let cutDocument (doc: OpenApiDocument) (opts: ScissorsOptions) : OpenApiDocument =
  let out = OpenApiDocument()
  out.Info <- doc.Info
  out.Servers <- doc.Servers
  let newPaths = OpenApiPaths()
  let referencedSchemas = HashSet<string>()

  for path, item in Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.documentPaths doc do
    match item with
    | null -> ()
    | pathItem ->
        let kept =
          Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.pathItemOperations pathItem
          |> List.choose (fun (method, op) ->
              if operationMatches opts path method op then Some (method, op) else None)

        match kept with
        | [] -> ()
        | ops ->
            let dict = Dictionary<HttpMethod, OpenApiOperation>()
            for method, op in ops do
              dict[method] <- op
              collectSchemaRefsFromOperation doc referencedSchemas op
            let pathItem = OpenApiPathItem()
            pathItem.Operations <- dict
            newPaths.Add(path, pathItem)

  out.Paths <- newPaths

  match Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.documentComponents doc with
  | None -> ()
  | Some _ ->
      out.Components <- OpenApiComponents()
      if isNull out.Components.Schemas then
        out.Components.Schemas <- Dictionary<string, IOpenApiSchema>()

      if opts.Transitive then
        let schemas = Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.documentSchemas doc
        for name in referencedSchemas do
          match Map.tryFind name schemas with
          | Some schema -> out.Components.Schemas[name] <- schema
          | None -> ()

  out

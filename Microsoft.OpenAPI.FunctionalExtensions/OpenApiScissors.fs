module Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors

open System
open System.Collections.Generic
open System.Net.Http
open Microsoft.OpenApi
open OpenApiSchemaAnalysis

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
    | ids, _ when String.IsNullOrWhiteSpace op.OperationId -> false
    | ids, _ -> ids |> List.exists (fun id -> String.Equals(id, op.OperationId, StringComparison.Ordinal))
  let byTag =
    match opts.IncludeTags, op with
    | [], _ -> false
    | _, null -> false
    | tags, _ when isNull op.Tags -> false
    | tags, _ -> op.Tags |> Seq.exists (fun t -> not (isNull t) && (tags |> List.exists (fun it -> String.Equals(it, t.Name, StringComparison.OrdinalIgnoreCase))))
  match anyFilterSpecified opts with
  | false -> true
  | true -> byPath || byOpId || byTag

let private trySchemaRefName (schema: IOpenApiSchema) : string option = tryGetReferenceId schema

let private unionInto (target: HashSet<string>) (items: seq<string>) =
  for x in items do target.Add x |> ignore

let rec private collectSchemaRefsFromSchema (doc: OpenApiDocument) (visited: HashSet<int>) (acc: HashSet<string>) (schema: IOpenApiSchema) : unit =
  match schema with
  | null -> ()
  | s ->
      let hash = Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s)
      if visited.Add hash then
        match trySchemaRefName s with
        | Some name ->
            acc.Add name |> ignore
            match doc.Components with
            | null -> ()
            | comp when not (isNull comp.Schemas) ->
                match comp.Schemas.TryGetValue name with
                | true, sub -> collectSchemaRefsFromSchema doc visited acc sub
                | _ -> ()
            | _ -> ()
        | None -> ()
        // descend
        match s.Properties with
        | null -> ()
        | props -> for kv in props do collectSchemaRefsFromSchema doc visited acc kv.Value
        match s.Items with
        | null -> ()
        | it -> collectSchemaRefsFromSchema doc visited acc it
        match s.AdditionalProperties with
        | null -> ()
        | ap -> collectSchemaRefsFromSchema doc visited acc ap
        let inline each (lst: IList<IOpenApiSchema>) = match lst with | null -> () | xs -> for x in xs do collectSchemaRefsFromSchema doc visited acc x
        each s.AllOf; each s.OneOf; each s.AnyOf

let private collectSchemasFromContent (doc: OpenApiDocument) (acc: HashSet<string>) (content: IDictionary<string, OpenApiMediaType>) =
  match content with
  | null -> ()
  | c ->
      for mt in c.Values do
        match mt with
        | null -> ()
        | m when isNull m.Schema -> ()
        | m -> collectSchemaRefsFromSchema doc (HashSet()) acc m.Schema

let private collectSchemaRefsFromOperation (doc: OpenApiDocument) (acc: HashSet<string>) (op: OpenApiOperation) =
  match op with
  | null -> ()
  | o ->
      match o.Parameters with
      | null -> ()
      | ps ->
          for p in ps do
            match p with
            | null -> ()
            | p when not (isNull p.Schema) -> collectSchemaRefsFromSchema doc (HashSet()) acc p.Schema
            | p when not (isNull p.Content) -> collectSchemasFromContent doc acc p.Content
            | _ -> ()
      match o.RequestBody with
      | null -> ()
      | rb -> collectSchemasFromContent doc acc rb.Content
      match o.Responses with
      | null -> ()
      | rs -> for r in rs.Values do match r with | null -> () | rr -> collectSchemasFromContent doc acc rr.Content

let cutDocument (doc: OpenApiDocument) (opts: ScissorsOptions) : OpenApiDocument =
  let out = OpenApiDocument()
  out.Info <- doc.Info
  out.Servers <- doc.Servers
  // paths
  let newPaths = OpenApiPaths()
  let referencedSchemas = HashSet<string>()
  match doc.Paths with
  | null -> ()
  | paths ->
      paths
      |> Seq.choose (fun kv ->
          let path = kv.Key
          match kv.Value with
          | null -> None
          | item when isNull item.Operations -> None
          | item ->
              let kept =
                item.Operations
                |> Seq.choose (fun opKv ->
                    let m = opKv.Key
                    let op = opKv.Value
                    if operationMatches opts path m op then Some (m, op) else None)
                |> Seq.toList
              match kept with
              | [] -> None
              | ops ->
                  let dict = System.Collections.Generic.Dictionary<HttpMethod, OpenApiOperation>()
                  ops |> List.iter (fun (m,op) -> dict[m] <- op; collectSchemaRefsFromOperation doc referencedSchemas op)
                  let ni = OpenApiPathItem()
                  ni.Operations <- dict
                  Some (path, ni))
      |> Seq.iter (fun (p, it) -> newPaths.Add(p, it))
  out.Paths <- newPaths
  // components
  match doc.Components with
  | null -> ()
  | comps ->
      out.Components <- OpenApiComponents()
      if isNull out.Components.Schemas then out.Components.Schemas <- System.Collections.Generic.Dictionary<string, IOpenApiSchema>()
      match opts.Transitive, comps.Schemas with
      | true, null -> ()
      | true, schemas ->
          for name in referencedSchemas do
            match schemas.TryGetValue name with
            | true, s -> out.Components.Schemas[name] <- s
            | _ -> ()
      | _ -> ()
  out



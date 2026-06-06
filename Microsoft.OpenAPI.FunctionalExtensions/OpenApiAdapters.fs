module Microsoft.OpenAPI.FunctionalExtensions.OpenApiAdapters

open System
open System.Collections.Generic
open System.Net.Http
open Microsoft.OpenApi
open OpenApiSchemaAnalysis

// ---------- Safe helpers over OpenAPI v2.0 object model (functional-first, null-safe) ----------

let inline private ofNull (value: 'T) = if isNull (box value) then None else Some value

let componentsSchemas (doc: OpenApiDocument) : IDictionary<string, IOpenApiSchema> option =
  match doc, doc.Components with
  | null, _ -> None
  | _, null -> None
  | _, comps when isNull comps.Schemas -> None
  | _, comps -> Some comps.Schemas

let tryGetComponentSchema (doc: OpenApiDocument) (name: string) : IOpenApiSchema option =
  match componentsSchemas doc with
  | None -> None
  | Some schemas ->
      match schemas.TryGetValue name with
      | true, s -> Some s
      | _ -> None

let trySchemaRefName (schema: IOpenApiSchema) : string option =
  OpenApiSchemaAnalysis.tryGetReferenceId schema

let schemaChildren (schema: IOpenApiSchema) : seq<IOpenApiSchema> =
  let yieldIfNotNull (x: 'a) (f: 'a -> seq<IOpenApiSchema>) = if isNull (box x) then Seq.empty else f x
  match schema with
  | null -> Seq.empty
  | s ->
      seq {
        yield! yieldIfNotNull s.Properties (fun props -> props |> Seq.choose (fun kv -> if isNull kv.Value then None else Some kv.Value))
        yield! match s.Items with null -> Seq.empty | it -> Seq.singleton it
        yield! match s.AdditionalProperties with null -> Seq.empty | ap -> Seq.singleton ap
        let each (lst: IList<IOpenApiSchema>) = if isNull lst then Seq.empty else lst |> Seq.choose (fun x -> if isNull x then None else Some x)
        yield! each s.AllOf
        yield! each s.OneOf
        yield! each s.AnyOf }

let schemasFromContent (content: IDictionary<string, OpenApiMediaType>) : seq<IOpenApiSchema> =
  seq {
    match content with
    | null -> ()
    | c -> for mt in c.Values do match mt with null -> () | m when isNull m.Schema -> () | m -> yield m.Schema }

let schemasFromOperation (op: OpenApiOperation) : seq<IOpenApiSchema> =
  seq {
    match op with
    | null -> ()
    | o ->
        // parameters
        match o.Parameters with
        | null -> ()
        | ps ->
            for p in ps do
              match p with
              | null -> ()
              | p when not (isNull p.Schema) -> yield p.Schema
              | p when not (isNull p.Content) -> yield! schemasFromContent p.Content
              | _ -> ()
        // request body
        match o.RequestBody with
        | null -> ()
        | rb -> yield! schemasFromContent rb.Content
        // responses
        match o.Responses with
        | null -> ()
        | rs -> for r in rs.Values do match r with null -> () | rr -> yield! schemasFromContent rr.Content }

let foldPaths (doc: OpenApiDocument) (folder: 's -> string * IOpenApiPathItem -> 's) (state: 's) : 's =
  match doc with
  | null -> state
  | d when isNull d.Paths -> state
  | d -> d.Paths |> Seq.fold (fun s kv -> folder s (kv.Key, kv.Value)) state

let foldOperations (doc: OpenApiDocument) (folder: 's -> string * HttpMethod * OpenApiOperation -> 's) (state: 's) : 's =
  foldPaths doc (fun s (path, item) ->
    match item with
    | null -> s
    | it when isNull (box (it.Operations)) -> s
    | it -> it.Operations |> Seq.fold (fun acc kv -> folder acc (path, kv.Key, kv.Value)) s) state



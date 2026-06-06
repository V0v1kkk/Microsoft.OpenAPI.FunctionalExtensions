module Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge

open System
open System.IO
open System.Collections.Generic
open Microsoft.OpenApi
open Microsoft.OpenApi.Reader
open ResultEx

type MergeError =
  | FileNotFound of string
  | ParseError of string
  | MergeConflict of string

let private loadDocument (path: string) : Result<OpenApiDocument, MergeError> =
  if not (File.Exists path) then Error (FileNotFound path) else
    try
      let settings = OpenApiReaderSettings()
      OpenApiReaderSettingsExtensions.AddYamlReader(settings)
      let text = File.ReadAllText path
      let rr = OpenApiDocument.Parse(text, format = OpenApiConstants.Yaml, settings = settings)
      let diag = rr.Diagnostic
      if not (isNull diag) && diag.Errors.Count > 0 then
        let msg = String.Join("; ", diag.Errors |> Seq.map (fun e -> e.Message + " at " + e.Pointer))
        Error (ParseError msg)
      else Ok rr.Document
    with ex -> Error (ParseError ex.Message)

let private mergeDictionaries<'K,'V when 'K : equality and 'K : comparison>
  (sectionName: string)
  (target: IDictionary<'K,'V>)
  (source: IDictionary<'K,'V>) : Result<unit, MergeError> =
  if isNull source then Ok () else
    source
    |> Seq.tryFind (fun kv -> target.ContainsKey kv.Key)
    |> function
    | Some kv -> Error (MergeConflict $"Duplicate key '{kv.Key}' in components/{sectionName}")
    | None ->
        for kv in source do target[kv.Key] <- kv.Value
        Ok ()

let private mergePathItems (target: OpenApiPaths) (source: OpenApiPaths) : Result<unit, MergeError> =
  if isNull source then Ok () else
    source
    |> Seq.tryFind (fun kv -> target.ContainsKey kv.Key)
    |> function
    | Some kv -> Error (MergeConflict $"Duplicate path '{kv.Key}'")
    | None ->
        for kv in source do target.Add(kv.Key, kv.Value)
        Ok ()

let private ensureComponents (doc: OpenApiDocument) =
  if isNull doc.Components then doc.Components <- OpenApiComponents()

let private mergeIntoBase (baseDoc: OpenApiDocument) (source: OpenApiDocument) : Result<unit, MergeError> =
  if isNull baseDoc.Paths then baseDoc.Paths <- OpenApiPaths()
  mergePathItems baseDoc.Paths source.Paths
  |> bind (fun () ->
      ensureComponents baseDoc
      ensureComponents source
      mergeDictionaries "schemas" baseDoc.Components.Schemas source.Components.Schemas
      |> bind (fun () -> mergeDictionaries "parameters" baseDoc.Components.Parameters source.Components.Parameters)
      |> bind (fun () -> mergeDictionaries "responses" baseDoc.Components.Responses source.Components.Responses)
      |> bind (fun () -> mergeDictionaries "requestBodies" baseDoc.Components.RequestBodies source.Components.RequestBodies)
      |> bind (fun () -> mergeDictionaries "headers" baseDoc.Components.Headers source.Components.Headers)
      |> bind (fun () -> mergeDictionaries "links" baseDoc.Components.Links source.Components.Links)
      |> bind (fun () -> mergeDictionaries "callbacks" baseDoc.Components.Callbacks source.Components.Callbacks)
      |> bind (fun () -> mergeDictionaries "examples" baseDoc.Components.Examples source.Components.Examples)
      |> bind (fun () -> mergeDictionaries "securitySchemes" baseDoc.Components.SecuritySchemes source.Components.SecuritySchemes))

let mergeDocuments (docs: OpenApiDocument list) : Result<OpenApiDocument, MergeError> =
  if docs.IsEmpty then Ok (OpenApiDocument()) else
    let baseDoc = docs.Head
    docs.Tail
    |> List.fold (fun acc doc ->
        acc |> bind (fun () -> mergeIntoBase baseDoc doc)) (Ok ())
    |> map (fun () -> baseDoc)

let mergeFiles (paths: string list) : Result<OpenApiDocument, MergeError> =
  let loaded =
    paths
    |> List.map loadDocument
    |> List.fold (fun acc r -> match acc, r with | Error e, _ -> Error e | _, Error e -> Error e | Ok xs, Ok d -> Ok (d::xs)) (Ok [])
  match loaded with
  | Error e -> Error e
  | Ok docs -> mergeDocuments (List.rev docs)

module Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge

open System
open System.IO
open System.Collections.Generic
open Microsoft.OpenApi
open Microsoft.OpenApi.Reader

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
  (target: IDictionary<'K,'V>)
  (source: IDictionary<'K,'V>) =
  if isNull source then () else
    for kv in source do
      if target.ContainsKey kv.Key then ()
      else target[kv.Key] <- kv.Value

let private mergePathItems (target: OpenApiPaths) (source: OpenApiPaths) =
  if isNull source then () else
    for kv in source do
      if target.ContainsKey kv.Key then
        // naive: skip conflicts; could deep-merge by methods later
        ()
      else target.Add(kv.Key, kv.Value)

let private ensureComponents (doc: OpenApiDocument) =
  if isNull doc.Components then doc.Components <- OpenApiComponents()

let mergeDocuments (docs: OpenApiDocument list) : OpenApiDocument =
  if docs.IsEmpty then OpenApiDocument() else
    let baseDoc = docs.Head
    for d in docs.Tail do
      // paths
      if isNull baseDoc.Paths then baseDoc.Paths <- OpenApiPaths()
      mergePathItems baseDoc.Paths d.Paths
      // components
      ensureComponents baseDoc
      ensureComponents d
      mergeDictionaries baseDoc.Components.Schemas d.Components.Schemas
      mergeDictionaries baseDoc.Components.Parameters d.Components.Parameters
      mergeDictionaries baseDoc.Components.Responses d.Components.Responses
      mergeDictionaries baseDoc.Components.RequestBodies d.Components.RequestBodies
      mergeDictionaries baseDoc.Components.Headers d.Components.Headers
      mergeDictionaries baseDoc.Components.Links d.Components.Links
      mergeDictionaries baseDoc.Components.Callbacks d.Components.Callbacks
      mergeDictionaries baseDoc.Components.Examples d.Components.Examples
      mergeDictionaries baseDoc.Components.SecuritySchemes d.Components.SecuritySchemes
    baseDoc

let mergeFiles (paths: string list) : Result<OpenApiDocument, MergeError> =
  let loaded =
    paths
    |> List.map loadDocument
    |> List.fold (fun acc r -> match acc, r with | Error e, _ -> Error e | _, Error e -> Error e | Ok xs, Ok d -> Ok (d::xs)) (Ok [])
  match loaded with
  | Error e -> Error e
  | Ok docs -> Ok (mergeDocuments (List.rev docs))



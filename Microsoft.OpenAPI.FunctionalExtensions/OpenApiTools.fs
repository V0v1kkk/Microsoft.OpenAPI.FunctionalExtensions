module OpenApiTools

open System
open System.Collections.Generic
open Microsoft.OpenApi
open System.Text.Json.Nodes
open StringExtensions
open SeqExtensions

let rec tryGetValue (value: obj) : Option<string> =
  match value with
  | null -> None
  | :? JsonNode as node ->
      match node with
      | :? JsonValue as v ->
          match v.TryGetValue<string>() with
          | true, s -> Some s
          | _ -> Some (v.ToJsonString())
      | :? JsonArray as arr ->
          arr |> Seq.map (fun x -> tryGetValue (x :> obj)) |> Seq.choose id |> Seq.toList |> joinAsLines |> Some
      | :? JsonObject as objn ->
          objn |> Seq.map (fun kv -> sprintf "%s=%s" kv.Key (tryGetValue (kv.Value :> obj) |> Option.defaultValue "")) |> joinAsLines |> Some
      | _ -> Some (node.ToJsonString())
  | :? string as s -> Some s
  | :? bool as b -> Some (string b)
  | :? int as i -> Some (string i)
  | :? float as f -> Some (string f)
  | _ -> Some (value.ToString())

let getExtensionValue (extensions: IDictionary<string, IOpenApiExtension>) extensionName =
  match extensions, extensionName with
  | _, extensionName when String.IsNullOrEmpty extensionName -> None
  | extensions, _ when extensions = null -> None
  | extensions, extensionName ->
    let isFound, foundValue = extensions.TryGetValue extensionName
    match isFound, foundValue with
    | false, _ -> None
    | _, (:? JsonNodeExtension as jext) -> jext.Node :> obj |> tryGetValue
    | _, anyValue -> anyValue :> obj |> tryGetValue

let operationHasExtensionWithTrueValue (operation: OpenApiOperation) operationName =
  match getExtensionValue operation.Extensions operationName with
  | Some extensionStringValue when extensionStringValue.icompare "true" -> true
  | _ -> false

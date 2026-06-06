[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.ExtensionAdapters

open System.Collections.Generic
open System.Text.Json.Nodes
open Microsoft.OpenApi
open StringExtensions

let extensions (dictionary: IDictionary<string, IOpenApiExtension>) : Map<string, IOpenApiExtension> =
  AdapterCore.readMap dictionary

let private jsonNodeToString (node: JsonNode) : string option =
  match node with
  | null -> None
  | :? JsonValue as value ->
      match value.TryGetValue<string>() with
      | true, text -> Some text
      | _ -> Some (value.ToJsonString())
  | node -> Some (node.ToJsonString())

let tryExtensionJsonNode (dictionary: IDictionary<string, IOpenApiExtension>) (name: string) : JsonNode option =
  match dictionary with
  | null -> None
  | extensions ->
      match extensions.TryGetValue name with
      | false, _ -> None
      | true, extensionValue ->
          match extensionValue with
          | null -> None
          | :? JsonNodeExtension as jsonExtension -> AdapterCore.ofObj jsonExtension.Node
          | _ -> None

let tryExtensionString (dictionary: IDictionary<string, IOpenApiExtension>) (name: string) : string option =
  match tryExtensionJsonNode dictionary name with
  | Some node -> jsonNodeToString node
  | None ->
      match dictionary with
      | null -> None
      | extensions ->
          match extensions.TryGetValue name with
          | false, _ -> None
          | true, extensionValue ->
              match extensionValue with
              | null -> None
              | :? JsonNodeExtension as jsonExtension ->
                  match jsonExtension.Node with
                  | null -> None
                  | node -> jsonNodeToString node
              | _ -> None

let private isTruthyString (value: string) =
  value.icompare "true" || value.icompare "1" || value.icompare "yes"

let extensionIsTruthy (dictionary: IDictionary<string, IOpenApiExtension>) (name: string) : bool =
  match tryExtensionString dictionary name with
  | None -> false
  | Some value -> isTruthyString value


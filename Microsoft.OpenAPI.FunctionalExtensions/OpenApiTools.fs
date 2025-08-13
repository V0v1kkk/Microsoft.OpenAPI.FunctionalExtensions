module OpenApiTools

open System
open System.Collections
open System.Collections.Generic
open Microsoft.OpenApi
open System.Text.Json.Nodes
open Microsoft.OpenApi
open StringExtensions
// open Results // legacy disabled
open SeqExtensions
open System.IO
open StringExtensions
open System.Collections.Generic

let rec private intersect a b =
    match a with
    |h::t -> match b with
             |h2::t2 -> 
                 if h=h2 then h::(intersect t t2)
                 else if h>h2 then intersect t b else intersect a t2
             |[] -> []
    |[] -> []
    
let private invert x = not x

//type Models.OpenApiOperation with
//  interface System.IComparable with
//       member x.CompareTo yobj =
//           match yobj with
//             | :? antiint as y -> anticompare x y
//             | _ -> invalidArg "yobj" "cannot compare values of different types" 
  //member s1.icompare(s2: string) = System.String.Equals(s1, s2, System.StringComparison.CurrentCultureIgnoreCase);
//type Operation = Operation of Models.OpenApiOperation

// OpenAPI v2 removed OpenApiAny model types; skip extension parsing helpers for now
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
  

let getExtensionValue (extensions:IDictionary<string,IOpenApiExtension>) extensionName = 
  match extensions, extensionName with
  | _, extensionName when (String.IsNullOrEmpty extensionName) -> Option.None
  | extensions, _ when extensions = null -> Option.None
  | extensions, extensionName ->
    let isFound, foundValue = extensions.TryGetValue extensionName
    match isFound, foundValue with
    | false, _ -> Option.None
    | _, (:? JsonNodeExtension as jext) -> jext.Node :> obj |> tryGetValue
    | _, anyValue -> anyValue :> obj |> tryGetValue
    

let operationHasExtensionWithTrueValue (operation:OpenApiOperation) operationName =
    let value = getExtensionValue operation.Extensions operationName
    match value with
    | Some extensionStringValue when extensionStringValue.icompare "true" -> true
    | _ -> false

let isInternalOperation operation =
  operationHasExtensionWithTrueValue operation "x-vendor-product-internal"
  
let isNotInternalOperation: OpenApiOperation -> bool = isInternalOperation >> invert
  
let isImplementedOperation operation =
  operationHasExtensionWithTrueValue operation "x-vendor-product-implemented"  
  

let getOperations (document:OpenApiDocument) =
  document.Paths
  |> Seq.map (fun pathKv -> pathKv.Value)
  |> Seq.collect (fun item -> item.Operations)
  |> Seq.map (fun item -> item.Value)
  
let getImplementedOperations document =
  getOperations document
  |> Seq.where isImplementedOperation
  
let getInternalOperations document =
  getOperations document
  |> Seq.where isInternalOperation
  
 
let getNotInternalImplementedOperations document =
  getOperations document
  |> Seq.filter isImplementedOperation
  |> Seq.filter isNotInternalOperation

let getSchemaByName (document:OpenApiDocument) (schemaName) =
  document.Components.Schemas
  |> Seq.tryFind (fun pair -> pair.Key = schemaName)
  |> function
    | None -> None
    | Some pair -> (pair.Value :?> OpenApiSchema) |> Some    
  
let rec traverseSchema<'T> (extract: OpenApiSchema -> 'T) (schema: OpenApiSchema) (propertyName: string) : seq<'T> =
  let currentResult = schema |> extract
  
  let innerPropertiesResult =
    match schema.Properties with
    | null -> Seq.empty
    | properties -> properties |> Seq.collect (fun property -> traverseSchema extract (property.Value :?> OpenApiSchema) property.Key)
    
  let allOfResult =
    match schema.AllOf with
    | null -> Seq.empty
    | allOfSchemas -> allOfSchemas |> Seq.collect (fun schema -> traverseSchema extract (schema :?> OpenApiSchema) propertyName)
     
  seq { yield currentResult; yield! innerPropertiesResult; yield! allOfResult }
  
module OpenApiTools

open System
open System.Collections
open System.Collections.Generic
open Microsoft.OpenApi
open Microsoft.OpenApi.Interfaces
open Microsoft.OpenApi.Models
open StringExtensions
open Results
open SeqExtensions
open System.IO
open StringExtensions

let rec intersect a b =
    match a with
    |h::t -> match b with
             |h2::t2 -> 
                 if h=h2 then h::(intersect t t2)
                 else if h>h2 then intersect t b else intersect a t2
             |[] -> []
    |[] -> []
    
let invert x = not x

let toSeqSafe (list: List<'a>) =
  match list with
  | null -> Seq.empty
  | _    -> seq list
  
//type Models.OpenApiOperation with
//  interface System.IComparable with
//       member x.CompareTo yobj =
//           match yobj with
//             | :? antiint as y -> anticompare x y
//             | _ -> invalidArg "yobj" "cannot compare values of different types" 
  //member s1.icompare(s2: string) = System.String.Equals(s1, s2, System.StringComparison.CurrentCultureIgnoreCase);
//type Operation = Operation of Models.OpenApiOperation

let rec tryGetValue (any:Any.IOpenApiAny):Option<string> = 
  
  let apiObjectToDictionary (object:Any.OpenApiObject) =
    [ for kvPair in object -> kvPair.Key, (tryGetValue kvPair.Value) ]
    |> seq
  
  let toSomeString (x:Object) = if x <> null then x |> (Convert.ToString >> Some) else None //todo: string to lower case

  let arrayToString array = 
    array
    |> seq 
    |> Seq.map (fun x -> tryGetValue x) 
    |> Seq.where (fun x -> x.IsSome) 
    |> Seq.map (fun x -> x.Value) 
    |> joinAsLines

  match any with
  | :? Any.OpenApiArray as arrayValue -> arrayValue |> arrayToString |> Some
  | :? Any.OpenApiBinary as binaryValue -> binaryValue.Value |> toSomeString
  | :? Any.OpenApiBoolean as booleanValue -> booleanValue.Value |> toSomeString
  | :? Any.OpenApiByte as byteValue -> byteValue.Value |> toSomeString
  | :? Any.OpenApiDate as dateValue -> dateValue.Value |> toSomeString
  | :? Any.OpenApiDateTime as dateValue -> dateValue.Value |> toSomeString
  | :? Any.OpenApiDouble as doubleValue -> doubleValue.Value |> toSomeString
  | :? Any.OpenApiFloat as floatValue -> floatValue.Value |> toSomeString
  | :? Any.OpenApiInteger as integerValue -> integerValue.Value |> toSomeString
  | :? Any.OpenApiLong as longValue -> longValue.Value |> toSomeString
  | :? Any.OpenApiNull -> None
  | :? Any.OpenApiObject as objectValue -> objectValue |> apiObjectToDictionary |> toSomeString  //todo: fix to string
  | :? Any.OpenApiPassword as passwordValue -> passwordValue.Value |> toSomeString
  | :? Any.OpenApiString as stringValue -> stringValue.Value |> toSomeString
  | _ -> None // todo: why incomplete matching? & replace to Failure
  

let getExtensionValue (extensions:System.Collections.Generic.IDictionary<string,IOpenApiExtension>) extensionName = 
  match extensions, extensionName with
  | _, extensionName when (String.IsNullOrEmpty extensionName) -> Option.None
  | extensions, _ when extensions = null -> Option.None
  | extensions, extensionName ->
    let isFound, foundValue = extensions.TryGetValue extensionName
    match isFound, foundValue with
    | isFound, _ when isFound = false -> Option.None
    | _, (:? Any.IOpenApiAny as anyValue) -> anyValue |> tryGetValue
    | _, _ -> Option.None  

let operationHasExtensionWithTrueValue (operation:OpenApiOperation) operationName =
    let value = getExtensionValue operation.Extensions operationName
    match value with
    | Some extensionStringValue when extensionStringValue.icompare "true" -> true
    | _ -> false

let isInternalOperation operation =
  operationHasExtensionWithTrueValue operation "x-vendor-vac-internal"
  
let isNotInternalOperation: OpenApiOperation -> bool = isInternalOperation >> invert
  
let isImplementedOperation operation =
  operationHasExtensionWithTrueValue operation "x-vendor-vac-internal"  
  

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
    | Some pair -> pair.Value |> Some
  
let rec traverseSchema<'T> (extract: OpenApiSchema -> 'T) (schema: OpenApiSchema) (propertyName: string) : seq<'T> =
  let currentResult = schema |> extract
  
  let innerPropertiesResult =
    match schema.Properties with
    | null -> Seq.empty
    | properties -> properties |> Seq.collect (fun property -> traverseSchema extract property.Value property.Key)
    
  let allOfResult =
    match schema.AllOf with
    | null -> Seq.empty
    | allOfSchemas -> allOfSchemas |> Seq.collect (fun schema -> traverseSchema extract schema propertyName)
     
  seq { yield currentResult; yield! innerPropertiesResult; yield! allOfResult }
  
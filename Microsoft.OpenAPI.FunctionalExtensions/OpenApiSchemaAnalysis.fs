module OpenApiSchemaAnalysis

open System
open Microsoft.OpenApi

/// DU for a light-weight schema classification that is renderer-agnostic
type SchemaClass =
  | Array of element: IOpenApiSchema option
  | Reference of id: string
  | Primitive of kind: string * format: string option * nullable: bool
  | Object

let private hasFlag (value: Nullable<JsonSchemaType>) (flag: JsonSchemaType) =
  value.HasValue && (value.Value &&& flag) = flag

let private tryParseComponentNameFromRef (referenceString: string) : string option =
  if String.IsNullOrWhiteSpace referenceString then None
  else
    let lastSlash = referenceString.LastIndexOf('/')
    if lastSlash >= 0 && lastSlash < referenceString.Length - 1 then
      Some (referenceString.Substring(lastSlash + 1))
    else
      Some (referenceString.TrimStart('#', '/'))

let tryGetReferenceId (schema: IOpenApiSchema) : string option =
  // OpenApiSchemaReference carries Id; concrete OpenApiSchema may also carry Id when component
  match schema with
  | :? OpenApiSchemaReference as r ->
      if not (String.IsNullOrWhiteSpace r.Id) then Some r.Id
      else
        // Try to access BaseOpenApiReference via the generic Reference property using reflection
        let t = r.GetType()
        let prop = t.GetProperty("Reference")
        let refObj = if isNull prop then null else prop.GetValue(r)
        match refObj with
        | :? BaseOpenApiReference as br ->
            if not (String.IsNullOrWhiteSpace br.Id) then Some br.Id
            else
              match tryParseComponentNameFromRef br.ReferenceV3 with
              | Some id -> Some id
              | None -> tryParseComponentNameFromRef br.ReferenceV2
        | _ -> None
  | s when not (String.IsNullOrWhiteSpace s.Id) -> Some s.Id
  | s ->
      // As a fallback, attempt the same reflection approach for other reference-holder types
      let t = s.GetType()
      let prop = t.GetProperty("Reference")
      let refObj = if isNull prop then null else prop.GetValue(s)
      match refObj with
      | :? BaseOpenApiReference as br ->
          if not (String.IsNullOrWhiteSpace br.Id) then Some br.Id
          else
            match tryParseComponentNameFromRef br.ReferenceV3 with
            | Some id -> Some id
            | None -> tryParseComponentNameFromRef br.ReferenceV2
      | _ -> None

let tryGetArrayItem (schema: IOpenApiSchema) : IOpenApiSchema option =
  match schema.Items with
  | null -> None
  | it -> Some it

let classifySchema (schema: IOpenApiSchema) : SchemaClass =
  if hasFlag schema.Type JsonSchemaType.Array || not (isNull schema.Items) then
    Array (tryGetArrayItem schema)
  else
    match tryGetReferenceId schema with
    | Some id -> Reference id
    | None ->
        if hasFlag schema.Type JsonSchemaType.Object then Object
        else
          let kind = if schema.Type.HasValue then string schema.Type.Value else ""
          let fmt = schema.Format |> Option.ofObj
          let isNullable = hasFlag schema.Type JsonSchemaType.Null
          if String.IsNullOrWhiteSpace kind && fmt.IsNone then Object
          else Primitive(kind, fmt, isNullable)

/// Unwraps nested arrays and returns the innermost element schema together with dimensions
let rec unwrapArrays (schema: IOpenApiSchema) : IOpenApiSchema * int =
  match classifySchema schema with
  | Array (Some el) -> unwrapArrays el |> fun (s, d) -> s, d + 1
  | _ -> schema, 0



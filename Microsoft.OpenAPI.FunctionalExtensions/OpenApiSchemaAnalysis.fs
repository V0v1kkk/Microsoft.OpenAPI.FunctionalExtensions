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

let tryGetReferenceId (schema: IOpenApiSchema) : string option =
  Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters.trySchemaReferenceId schema

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

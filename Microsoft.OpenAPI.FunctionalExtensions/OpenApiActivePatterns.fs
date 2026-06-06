module Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns

open System
open System.Collections.Generic
open Microsoft.OpenApi
open OpenApiSchemaAnalysis

type CompositionKind =
  | AllOf
  | OneOf
  | AnyOf

let private hasTypeFlag (schemaType: Nullable<JsonSchemaType>) (flag: JsonSchemaType) =
  schemaType.HasValue && (schemaType.Value &&& flag) = flag

let private readNonEmptyComposition (kind: CompositionKind) (schemas: IList<IOpenApiSchema>) =
  match schemas with
  | null -> None
  | list when list.Count = 0 -> None
  | list ->
      let items =
        list
        |> Seq.choose (fun schema ->
            if isNull (box schema) then None else Some schema)
        |> Seq.toList

      if List.isEmpty items then None else Some (kind, items)

let (|SchemaRef|_|) (schema: IOpenApiSchema) : string option =
  tryGetReferenceId schema

let (|ArraySchema|_|) (schema: IOpenApiSchema) : IOpenApiSchema option =
  match schema with
  | null -> None
  | s ->
      let isArrayType = hasTypeFlag s.Type JsonSchemaType.Array
      let hasItems = not (isNull s.Items)

      if (isArrayType || hasItems) && hasItems then
        Some s.Items
      else
        None

let (|ObjectSchema|_|) (schema: IOpenApiSchema) : IDictionary<string, IOpenApiSchema> option =
  match schema with
  | null -> None
  | s ->
      match s.Properties with
      | null -> None
      | properties when properties.Count = 0 -> None
      | properties -> Some properties

let (|ComposedSchema|_|) (schema: IOpenApiSchema) : (CompositionKind * IOpenApiSchema list) option =
  match schema with
  | null -> None
  | s ->
      [ readNonEmptyComposition AllOf s.AllOf
        readNonEmptyComposition OneOf s.OneOf
        readNonEmptyComposition AnyOf s.AnyOf ]
      |> List.tryPick id

let (|NotNull|_|) (value: 'T when 'T: not struct) : 'T option =
  if isNull (box value) then None else Some value

let (|NullableType|NonNullableType|) (schemaType: Nullable<JsonSchemaType>) =
  if not schemaType.HasValue then
    NonNullableType schemaType
  elif hasTypeFlag schemaType JsonSchemaType.Null then
    let withoutNull = schemaType.Value &&& ~~~JsonSchemaType.Null
    NullableType (Nullable withoutNull)
  else
    NonNullableType schemaType

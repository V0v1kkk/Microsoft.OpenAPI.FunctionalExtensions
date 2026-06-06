[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters

open System.Text.Json.Nodes
open Microsoft.OpenApi

let schemaTitle (schema: IOpenApiSchema) : string option =
  match schema with
  | null -> None
  | s -> AdapterCore.ofObj s.Title

let schemaDescription (schema: IOpenApiSchema) : string option =
  match schema with
  | null -> None
  | s -> AdapterCore.ofObj s.Description

let schemaType (schema: IOpenApiSchema) : JsonSchemaType option =
  match schema with
  | null -> None
  | s -> AdapterCore.ofNullable s.Type

let schemaFormat (schema: IOpenApiSchema) : string option =
  match schema with
  | null -> None
  | s -> AdapterCore.ofObj s.Format

let schemaIsNullable (schema: IOpenApiSchema) : bool =
  match schema with
  | null -> false
  | s when s.Type.HasValue -> (s.Type.Value &&& JsonSchemaType.Null) = JsonSchemaType.Null
  | _ -> false

let schemaProperties (schema: IOpenApiSchema) : Map<string, IOpenApiSchema> =
  match schema with
  | null -> Map.empty
  | s -> AdapterCore.readMap s.Properties

let schemaRequired (schema: IOpenApiSchema) : Set<string> =
  match schema with
  | null -> Set.empty
  | s -> AdapterCore.readSet s.Required

let schemaItems (schema: IOpenApiSchema) : IOpenApiSchema option =
  match schema with
  | null -> None
  | s -> AdapterCore.ofObj s.Items

let schemaAllOf (schema: IOpenApiSchema) : IOpenApiSchema list =
  match schema with
  | null -> []
  | s -> AdapterCore.readSeq s.AllOf

let schemaOneOf (schema: IOpenApiSchema) : IOpenApiSchema list =
  match schema with
  | null -> []
  | s -> AdapterCore.readSeq s.OneOf

let schemaAnyOf (schema: IOpenApiSchema) : IOpenApiSchema list =
  match schema with
  | null -> []
  | s -> AdapterCore.readSeq s.AnyOf

let schemaNot (schema: IOpenApiSchema) : IOpenApiSchema option =
  match schema with
  | null -> None
  | s -> AdapterCore.ofObj s.Not

let schemaAdditionalProperties (schema: IOpenApiSchema) : IOpenApiSchema option =
  match schema with
  | null -> None
  | s -> AdapterCore.ofObj s.AdditionalProperties

let schemaEnum (schema: IOpenApiSchema) : JsonNode list =
  match schema with
  | null -> []
  | s -> AdapterCore.readSeq s.Enum

let schemaReadOnly (schema: IOpenApiSchema) : bool =
  match schema with
  | null -> false
  | s -> s.ReadOnly

let schemaWriteOnly (schema: IOpenApiSchema) : bool =
  match schema with
  | null -> false
  | s -> s.WriteOnly

let schemaDeprecated (schema: IOpenApiSchema) : bool =
  match schema with
  | null -> false
  | s -> s.Deprecated

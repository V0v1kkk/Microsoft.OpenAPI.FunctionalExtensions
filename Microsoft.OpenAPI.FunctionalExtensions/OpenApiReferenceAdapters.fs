[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters

open System
open Microsoft.OpenApi

let private tryParseComponentNameFromRef (referenceString: string) : string option =
  if String.IsNullOrWhiteSpace referenceString then
    None
  else
    let lastSlash = referenceString.LastIndexOf('/')

    if lastSlash >= 0 && lastSlash < referenceString.Length - 1 then
      Some (referenceString.Substring(lastSlash + 1))
    else
      Some (referenceString.TrimStart('#', '/'))

let private normalizeReferenceId (id: string) : string option =
  if String.IsNullOrWhiteSpace id then None else tryParseComponentNameFromRef id

let trySchemaReferenceId (schema: IOpenApiSchema) : string option =
  match schema with
  | null -> None
  | :? OpenApiSchemaReference as reference ->
      match normalizeReferenceId reference.Id with
      | Some id -> Some id
      | None ->
          match reference.Reference with
          | null -> None
          | ref ->
              match normalizeReferenceId ref.Id with
              | Some id -> Some id
              | None -> normalizeReferenceId ref.ReferenceV3
  | schema -> normalizeReferenceId schema.Id

let referencePointer (id: string) : string = $"#/components/schemas/{id}"

let tryResolveSchemaReference (document: OpenApiDocument) (schema: IOpenApiSchema) : IOpenApiSchema option =
  match schema with
  | null -> None
  | unresolved ->
      match trySchemaReferenceId unresolved with
      | None -> Some unresolved
      | Some id ->
          match document with
          | null -> None
          | doc ->
              match doc.Components with
              | null -> None
              | components ->
                  match components.Schemas with
                  | null -> None
                  | schemas ->
                      match schemas.TryGetValue id with
                      | true, resolved -> Some resolved
                      | _ -> None

let isUnresolvedReference (schema: IOpenApiSchema) : bool =
  match schema with
  | :? OpenApiSchemaReference as reference -> reference.UnresolvedReference
  | _ -> false

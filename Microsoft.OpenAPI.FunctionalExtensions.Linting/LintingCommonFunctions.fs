module CMicrosoft.OpenAPI.FunctionalExtensions.Linting.LintingCommonFunctions

open System
open ResultEx
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiAdapters

let getSchemaByName (document: OpenApiDocument) (schemaName: string) =
  match tryGetComponentSchema document schemaName with
  | Some schema -> schema |> Ok
  | None -> "Schema with specified name didn't found." |> AnalyzingError |> Error

module CMicrosoft.OpenAPI.FunctionalExtensions.Linting.LintingCommonFunctions

open System
open ResultEx
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

let getSchemaByName (document:OpenApiDocument) (schemaName:String) =
  schemaName
  |> OpenApiTools.getSchemaByName document
  |> function
  | Some schema -> schema |> Ok
  | None -> "Schema with specified name didn't found." |> AnalyzingError |> Error

module CMicrosoft.OpenAPI.FunctionalExtensions.Linting.LintingCommonFunctions

open System
open Results
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

let getSchemaByName (document:Models.OpenApiDocument) (schemaName:String) =
  schemaName
  |> OpenApiTools.getSchemaByName document
  |> function
    | Some schema -> schema |> Success
    | None -> "Schema with specified name didn't found." |> AnalyzingError |> Failure

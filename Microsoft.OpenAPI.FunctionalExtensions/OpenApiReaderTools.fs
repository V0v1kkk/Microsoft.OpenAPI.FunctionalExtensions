module Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools

open System
open System.IO
open ResultEx
open SeqExtensions
open Microsoft.OpenApi
open Microsoft.OpenApi.Reader
open Microsoft.OpenAPI.FunctionalExtensions.Readers.Types

// Get specification from string

let validateFilePath filePath = 
  let isFileExist = File.Exists filePath;
  match isFileExist with
  | true -> filePath |> SpecificationFilePath |> Ok
  | false -> "Specification file doesn't exist." |> FileNotFound |> Error


let readFileText filePath =
  let readContent = fun (SpecificationFilePath path) -> path |> File.ReadAllText
  
  let readContentExceptionHandler exn =
    $"Error on read specification file: %s{exn.ToString()}" |> FileReadError
  
  tryCatch readContent readContentExceptionHandler filePath

let diagnosticsToErrorSting (error: Microsoft.OpenApi.OpenApiError) =
  let message = sprintf "Error on specification parsing %s." error.Message
  (message, error.Pointer) |> Microsoft.OpenAPI.FunctionalExtensions.Readers.Types.OpenApiError

let convertToSpecification fileContent =
  // OpenAPI.NET v2+: use OpenApiDocument.Parse with optional YAML support
  let settings = OpenApiReaderSettings()
  OpenApiReaderSettingsExtensions.AddYamlReader(settings)
  try
    let rr: ReadResult = OpenApiDocument.Parse(fileContent, format = OpenApiConstants.Yaml, settings = settings)
    let diag = rr.Diagnostic
    if not (isNull (box diag)) && diag.Errors.Count > 0 then
      diag.Errors
      |> Seq.map diagnosticsToErrorSting
      |> OpenApiErrors
      |> Error
    else
      Ok rr.Document
  with ex ->
    ($"Error on specification parsing {ex.Message}") |> FileReadError |> Error


let readSpecification (filePath: string) = 
  filePath
  |> fun path -> path.Trim [| '"' |]
  |> validateFilePath
  |> Result.bind readFileText
  |> Result.bind convertToSpecification
module Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools

open System
open System.IO
open Results
open SeqExtensions
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Readers.Types

// Get specification from string

let validateFilePath filePath = 
  let isFileExist = File.Exists filePath;
  match isFileExist with
  | true -> filePath |> SpecificationFilePath |> Success
  | false -> "Specification file doesn't exist." |> FileNotFound |> Failure


let readFileText filePath =
  let readContent = fun (SpecificationFilePath path) -> path |> File.ReadAllText
  
  let readContentExceptionHandler exn =
    $"Error on read specification file: %s{exn.ToString()}" |> FileReadError
  
  tryCatch readContent readContentExceptionHandler filePath

let diagnosticsToErrorSting (error:Models.OpenApiError) =
  let message = $"Error on specification parsing %s{error.Message}."
  (message, error.Pointer) |> OpenApiError

let convertToSpecification fileContent =
  let mutable defineSettings = Readers.OpenApiReaderSettings()
  defineSettings.ReferenceResolution <- Readers.ReferenceResolutionSetting.ResolveLocalReferences
  let reader = Microsoft.OpenApi.Readers.OpenApiStringReader defineSettings
  
  fileContent
  |> reader.Read
  |> function
    | _, diagnostic when diagnostic.Errors.Count > 0 -> 
      diagnostic.Errors 
      |> Seq.map diagnosticsToErrorSting 
      |> OpenApiErrors
      |> Failure
    | document, _ -> document |> Success 


let readSpecification (filePath: string) = 
  filePath
  |> fun path -> path.Trim [| '"' |]
  |> validateFilePath
  >>= readFileText
  >>= convertToSpecification
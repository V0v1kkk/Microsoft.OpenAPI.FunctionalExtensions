module Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools

open System
open System.IO
open Results
open SeqExtensions
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Types

// Get specification from string

let validateFilePath filePath = 
  let isFileExist = File.Exists filePath;
  match isFileExist with
  | true -> filePath |> SpecificationFilePath |> Success
  | false -> "Specification file doesn't exist." |> AnalyzingError |> Failure


let readFileText filePath =
  let readContent = fun (SpecificationFilePath wrappedPath) -> wrappedPath |> System.IO.File.ReadAllText
  
  let readContentExceptionHandler ex = 
    ("Error on read specification file", ex) |> AnalyzingException
  
  tryCatch readContent readContentExceptionHandler filePath

let diagnosticsToErrorSting (error:Models.OpenApiError) =
  sprintf "Error on specification parsing %s. Position: %s" error.Message error.Pointer


let convertToSpecification fileContent =
  let mutable defineSettings = Readers.OpenApiReaderSettings()
  defineSettings.ReferenceResolution <- Readers.ReferenceResolutionSetting.ResolveLocalReferences
  let reader = Microsoft.OpenApi.Readers.OpenApiStringReader defineSettings
  
  fileContent
  |> reader.Read
  |> function
    | (_, diagnostic) when diagnostic.Errors.Count > 0 -> 
      diagnostic.Errors 
      |> Seq.map diagnosticsToErrorSting 
      |> joinAsLines 
      |> AnalyzingError 
      |> Failure
    | (document, _) -> document |> Success 


let readSpecification (filePath: string) = 
  filePath
  |> fun path -> path.Trim [| '"' |]
  |> validateFilePath
  >>= readFileText
  >>= convertToSpecification


let getSchemaByName (document:Models.OpenApiDocument) (schemaName:String) =
  schemaName
  |> OpenApiTools.getSchemaByName document
  |> function
    | Some schema -> schema |> Success
    | None -> "Schema with specified name didn't found." |> AnalyzingError |> Failure
  
  


module Microsoft.OpenAPI.FunctionalExtensions.Readers.Types

open Microsoft.OpenApi
// Using FSharp.Core Result in consumer modules; keep this file Result-agnostic.

type SpecificationFilePath = SpecificationFilePath of string

type OpenApiError = OpenApiError of Message:string * Location:string

type ReaderError =
    | FileNotFound of string
    | FileReadError of string
    | OpenApiErrors of OpenApiError seq



type ReadSpecification = SpecificationFilePath -> Result<OpenApiDocument,ReaderError>

//of CaseDefinition:string * Exception:System.Exception
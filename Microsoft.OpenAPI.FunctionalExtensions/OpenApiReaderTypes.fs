module Microsoft.OpenAPI.FunctionalExtensions.Readers.Types

open Microsoft.OpenApi.Models
open Results

type SpecificationFilePath = SpecificationFilePath of string

type OpenApiError = OpenApiError of Message:string * Location:string

type ReaderError =
    | FileNotFound of string
    | FileReadError of string
    | OpenApiErrors of OpenApiError seq



type ReadSpecification = SpecificationFilePath -> Result<OpenApiDocument,ReaderError>

//of CaseDefinition:string * Exception:System.Exception
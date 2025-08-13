open System
open Argu
open System.IO
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenApi
open ResultEx

type OpenApiVersion =
    | V2
    | V3

type Arguments =
    | [<Mandatory>] Specification_Path of path:string
    | [<Mandatory>] Output_Specification_Version of OpenApiVersion
    | Output_Path of string
    // todo: add suppress errors flag

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Specification_Path _ -> "Specify a path to json or yaml file with the OpenAPI specification (any version)."
            | Output_Specification_Version _ -> "Specify output specification version."
            | Output_Path _ -> "You can specify output specification path. Otherwise the converted specification will be saved to the folder with the original specification."

[<EntryPoint>]
let main argv = 
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<Arguments>(programName = "openapi-converter", errorHandler = errorHandler)
    let results = parser.ParseCommandLine argv
    
    let outputVersion = results.GetResult Output_Specification_Version
    let specificationPath = results.GetResult Specification_Path
    
    let outputDefaultValue = Path.ChangeExtension(specificationPath, ".converted.json")
    let outputPath = results.GetResult (Output_Path, defaultValue = outputDefaultValue)
        
       
    let serializeAsv2 (document:OpenApiDocument) name =
        use fileWriter = File.CreateText(name)        
        document.SerializeAsV2(new OpenApiJsonWriter(fileWriter));
        fileWriter.Flush()
        
    let serializeAsv3 (document:OpenApiDocument) name =
        use fileWriter = File.CreateText(name)        
        document.SerializeAsV3(new OpenApiJsonWriter(fileWriter));
        fileWriter.Flush()
        
(*    let serializeTest (serializer:OpenApiDocument -> OpenApiJsonWriter -> unit) (document:OpenApiDocument) name =
        use fileWriter = File.CreateText(name)        
        document.SerializeAsV3(new OpenApiJsonWriter(fileWriter));
        fileWriter.Flush()*)

    let serialize (version:OpenApiVersion) name (document:OpenApiDocument) =
        match version with
        | V2 -> serializeAsv2 document name
        | V3 -> serializeAsv3 document name
        
        
    let serializeToSpecified = serialize outputVersion outputPath
        
    let readSpecificationResult = readSpecification specificationPath
    match readSpecificationResult with
        | Ok document -> serializeToSpecified document 
        | Error _ -> ignore() // todo: add error output
    
    0
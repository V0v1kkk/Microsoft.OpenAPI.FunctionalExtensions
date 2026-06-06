module Microsoft.OpenAPI.FunctionalExtensions.OpenApiWriterTools

open System.IO
open Microsoft.OpenApi
open ResultEx

let saveDocument (doc: OpenApiDocument) (outPath: string) : Result<unit, string> =
    tryCatch
        (fun () ->
            use sw = new StreamWriter(outPath)
            let ext = Path.GetExtension(outPath).ToLowerInvariant()
            if ext = ".json" then
                let w = new OpenApiJsonWriter(sw)
                doc.SerializeAsV3(w)
                sw.Flush()
            else
                let w = new OpenApiYamlWriter(sw)
                doc.SerializeAsV3(w)
                sw.Flush())
        (fun ex -> $"Failed to save document to '{outPath}': {ex.Message}")
        ()

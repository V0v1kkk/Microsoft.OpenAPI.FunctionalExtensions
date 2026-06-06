module Microsoft.OpenAPI.FunctionalExtensions.OpenApiWriterTools

open System.IO
open Microsoft.OpenApi
open Microsoft.OpenApi.Writers

let saveDocument (doc: OpenApiDocument) (outPath: string) =
    use sw = new StreamWriter(outPath)
    let ext = Path.GetExtension(outPath).ToLowerInvariant()
    if ext = ".json" then
        let w = new OpenApiJsonWriter(sw)
        doc.SerializeAsV3(w)
        sw.Flush()
    else
        let w = new OpenApiYamlWriter(sw)
        doc.SerializeAsV3(w)
        sw.Flush()



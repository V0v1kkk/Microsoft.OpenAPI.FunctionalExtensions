module Microsoft.OpenAPI.FunctionalExtensions.Tests.Adapters

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiAdapters
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools

[<Test>]
let ``schemaChildren returns items and properties`` () =
    match readSpecification "Samples/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read: %A{e}")
    | Ok doc ->
        let pets = doc.Components.Schemas.["Pets"]
        let children = schemaChildren pets |> Seq.toList
        Assert.That(children.IsEmpty, Is.False)

[<Test>]
let ``schemasFromOperation returns response schema refs`` () =
    match readSpecification "Samples/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read: %A{e}")
    | Ok doc ->
        let ops = ResizeArray<_>()
        foldOperations doc (fun s (path,m,op) -> ops.Add (path,m,op); s) () |> ignore
        let anySchemas =
            ops
            |> Seq.collect (fun (_,_,op) -> schemasFromOperation op)
            |> Seq.isEmpty
            |> not
        Assert.That(anySchemas, Is.True)


module Microsoft.OpenAPI.FunctionalExtensions.Tests.Adapters

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools

let private schemaChildren (schema: IOpenApiSchema) =
    [
        yield! Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaProperties schema |> Map.values
        yield! Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaItems schema |> Option.toList
        yield! Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAdditionalProperties schema |> Option.toList
        yield! Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAllOf schema
        yield! Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaOneOf schema
        yield! Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaAnyOf schema
    ]

[<Test>]
let ``schemaChildren returns items and properties`` () =
    match readSpecification "Samples/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read: %A{e}")
    | Ok doc ->
        match Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.tryComponentSchema doc "Pets" with
        | None -> Assert.Fail("Expected Pets component schema")
        | Some pets ->
            let children = schemaChildren pets
            Assert.That(children.IsEmpty, Is.False)

[<Test>]
let ``schemasFromOperation returns response schema refs`` () =
    match readSpecification "Samples/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read: %A{e}")
    | Ok doc ->
        let anySchemas =
            Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.allOperations doc
            |> List.collect (fun (_, _, op) ->
                Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters.schemasFromOperation op)
            |> List.isEmpty
            |> not
        Assert.That(anySchemas, Is.True)

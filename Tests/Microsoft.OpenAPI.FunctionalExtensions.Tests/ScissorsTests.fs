module Microsoft.OpenAPI.FunctionalExtensions.Tests.Scissors

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors

[<Test>]
let ``cut by tag keeps matching routes and schemas`` () =
    match readSpecification "Samples/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read: %A{e}")
    | Ok doc ->
        let opts = { ScissorsOptions.Empty with IncludeTags = ["pets"] }
        let cut = cutDocument doc opts
        Assert.That(cut.Paths.ContainsKey("/pets"), Is.True)
        // Non-tagged path should be absent
        // Note: petstore has only 'pets' tags in sample
        Assert.That(cut.Components.Schemas.ContainsKey("Pet"), Is.True)

[<Test>]
let ``cut by path keeps owners and owner schema when merged`` () =
    // Merge two specs first
    match readSpecification "Samples/petstore-extended.yaml" with
    | Error e -> Assert.Fail($"Failed to read ext: %A{e}")
    | Ok doc2 ->
        match readSpecification "Samples/petstore.yaml" with
        | Error e -> Assert.Fail($"Failed to read base: %A{e}")
        | Ok doc1 ->
            // shallow merge (paths + components) for test
            let merged = OpenApiDocument()
            merged.Info <- doc1.Info
            merged.Paths <- doc1.Paths
            if merged.Paths = null then merged.Paths <- OpenApiPaths()
            for kv in doc2.Paths do merged.Paths[kv.Key] <- kv.Value
            merged.Components <- OpenApiComponents()
            if isNull merged.Components.Schemas then merged.Components.Schemas <- System.Collections.Generic.Dictionary<string, IOpenApiSchema>()
            for kv in doc1.Components.Schemas do merged.Components.Schemas[kv.Key] <- kv.Value
            for kv in doc2.Components.Schemas do merged.Components.Schemas[kv.Key] <- kv.Value
            let opts = { ScissorsOptions.Empty with IncludePaths = ["/owners"] }
            let cut = cutDocument merged opts
            Assert.That(cut.Paths.ContainsKey("/owners"), Is.True)
            Assert.That(cut.Components.Schemas.ContainsKey("Owner"), Is.True)

[<Test>]
let ``cut by operationId keeps route showPetById`` () =
    match readSpecification "Samples/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read: %A{e}")
    | Ok doc ->
        let opts = { ScissorsOptions.Empty with IncludeOperationIds = ["showPetById"] }
        let cut = cutDocument doc opts
        Assert.That(cut.Paths.ContainsKey("/pets/{petId}"), Is.True)
        Assert.That(cut.Paths.Count, Is.EqualTo(1))
        // Pet and Error schemas referenced by response should be included by transitive
        Assert.That(cut.Components.Schemas.ContainsKey("Pet"), Is.True)
        Assert.That(cut.Components.Schemas.ContainsKey("Error"), Is.True)

[<Test>]
let ``cut by path substring 'pets' keeps both pets routes`` () =
    match readSpecification "Samples/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read: %A{e}")
    | Ok doc ->
        let opts = { ScissorsOptions.Empty with IncludePaths = ["/pets"] }
        let cut = cutDocument doc opts
        Assert.That(cut.Paths.ContainsKey("/pets"), Is.True)
        Assert.That(cut.Paths.ContainsKey("/pets/{petId}"), Is.True)

[<Test>]
let ``cut with transitive false keeps paths but no components`` () =
    match readSpecification "Samples/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read: %A{e}")
    | Ok doc ->
        let opts = { ScissorsOptions.Empty with IncludeTags = ["pets"]; Transitive = false }
        let cut = cutDocument doc opts
        Assert.That(cut.Paths.ContainsKey("/pets"), Is.True)
        Assert.That(isNull cut.Components |> not && (isNull cut.Components.Schemas || cut.Components.Schemas.Count = 0), Is.True)

[<Test>]
let ``cut with no matches yields empty spec`` () =
    match readSpecification "Samples/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read: %A{e}")
    | Ok doc ->
        let opts = { ScissorsOptions.Empty with IncludeOperationIds = ["__no_such_op__"] }
        let cut = cutDocument doc opts
        Assert.That(cut.Paths = null || cut.Paths.Count = 0, Is.True)
        Assert.That(cut.Components = null || cut.Components.Schemas = null || cut.Components.Schemas.Count = 0, Is.True)


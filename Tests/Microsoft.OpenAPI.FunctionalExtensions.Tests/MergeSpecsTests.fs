module Microsoft.OpenAPI.FunctionalExtensions.Tests.MergeSpecs

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools

[<Test>]
let ``mergeDocuments with empty list returns empty document`` () =
    match mergeDocuments [] with
    | Error e -> Assert.Fail($"Unexpected error: %A{e}")
    | Ok doc -> Assert.That(doc, Is.Not.Null)

[<Test>]
let ``mergeDocuments with single document returns identity`` () =
    match readSpecification "Specifications/merge-base.yaml" with
    | Error e -> Assert.Fail($"Failed to read base: %A{e}")
    | Ok doc ->
        match mergeDocuments [ doc ] with
        | Error err -> Assert.Fail($"Unexpected error: %A{err}")
        | Ok merged ->
            Assert.That(merged.Paths.ContainsKey("/items"), Is.True)
            Assert.That(merged.Components.Schemas.ContainsKey("Item"), Is.True)

[<Test>]
let ``mergeFiles reports MergeConflict on duplicate path`` () =
    match mergeFiles [ "Specifications/merge-base.yaml"; "Specifications/merge-conflict-path.yaml" ] with
    | Ok _ -> Assert.Fail("Expected merge conflict on duplicate path")
    | Error (MergeConflict message) -> Assert.That(message, Does.Contain("/items"))
    | Error other -> Assert.Fail($"Expected MergeConflict, got %A{other}")

[<Test>]
let ``mergeFiles reports MergeConflict on duplicate schema key`` () =
    match mergeFiles [ "Specifications/merge-base.yaml"; "Specifications/merge-conflict-schema.yaml" ] with
    | Ok _ -> Assert.Fail("Expected merge conflict on duplicate schema")
    | Error (MergeConflict message) -> Assert.That(message, Does.Contain("Item"))
    | Error other -> Assert.Fail($"Expected MergeConflict, got %A{other}")

[<Test>]
let ``mergeFiles reports FileNotFound for non-existent file`` () =
    match mergeFiles [ "Specifications/does-not-exist.yaml" ] with
    | Ok _ -> Assert.Fail("Expected FileNotFound error")
    | Error (FileNotFound path) -> Assert.That(path, Does.Contain("does-not-exist.yaml"))
    | Error other -> Assert.Fail($"Expected FileNotFound, got %A{other}")

[<Test>]
let ``mergeFiles reports ParseError for invalid YAML`` () =
    let invalidPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "invalid-openapi-merge.yaml")
    System.IO.File.WriteAllText(invalidPath, "openapi: [not: valid: yaml: structure")

    try
        match mergeFiles [ invalidPath ] with
        | Ok _ -> Assert.Fail("Expected ParseError for invalid YAML")
        | Error (ParseError _) -> ()
        | Error other -> Assert.Fail($"Expected ParseError, got %A{other}")
    finally
        if System.IO.File.Exists invalidPath then System.IO.File.Delete invalidPath |> ignore

[<Test>]
let ``merge two petstores results in combined paths and schemas`` () =
    let baseSpec = "Samples/petstore.yaml"
    let extSpec = "Samples/petstore-extended.yaml"
    match mergeFiles [ baseSpec; extSpec ] with
    | Error e -> Assert.Fail($"Failed to merge: %A{e}")
    | Ok doc ->
        // paths include /pets from base and /owners from ext
        let hasPets = not (isNull doc.Paths) && doc.Paths.ContainsKey("/pets")
        let hasOwners = not (isNull doc.Paths) && doc.Paths.ContainsKey("/owners")
        Assert.That(hasPets, Is.True)
        Assert.That(hasOwners, Is.True)
        // schemas include Pet from base and Owner from ext
        let hasPet = not (isNull doc.Components) && not (isNull doc.Components.Schemas) && doc.Components.Schemas.ContainsKey("Pet")
        let hasOwner = not (isNull doc.Components) && not (isNull doc.Components.Schemas) && doc.Components.Schemas.ContainsKey("Owner")
        Assert.That(hasPet, Is.True)
        Assert.That(hasOwner, Is.True)


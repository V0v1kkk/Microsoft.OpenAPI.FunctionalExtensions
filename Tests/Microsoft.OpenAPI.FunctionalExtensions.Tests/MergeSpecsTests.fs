module Microsoft.OpenAPI.FunctionalExtensions.Tests.MergeSpecs

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge

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


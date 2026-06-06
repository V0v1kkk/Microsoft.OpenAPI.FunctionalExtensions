// Runnable demonstrations of Functional.Microsoft.OpenAPI.Extensions.
// Each section is self-contained — copy the patterns into your own projects.

open System
open System.IO
open System.Net.Http
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors
open Microsoft.OpenAPI.FunctionalExtensions.Linting
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types
open Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport
open OpenApiLinksTraversal
open OpenApiOperationsTraversal
open OpenApiTraversal

/// Resolve sample files copied to the build output directory.
let samplePath (fileName: string) =
    Path.Combine(AppContext.BaseDirectory, "Samples", fileName)

let schemaNodeLabel (node: SchemaNode) =
    match node.Title with
    | Some title when not (String.IsNullOrWhiteSpace title) -> title
    | _ ->
        if node.Id.StartsWith("#/components/schemas/", StringComparison.Ordinal) then
            node.Id.Split('/') |> Array.last
        else
            node.Id

let nullableSuffix (nullable: bool option) =
    match nullable with
    | Some true -> "?"
    | _ -> ""

let printSection title =
    printfn ""
    printfn "=== %s ===" title

// ---------------------------------------------------------------------------
// Section 1: Reading a spec
// ---------------------------------------------------------------------------

let demonstrateReading () =
    printSection "1. Reading a specification"

    // Load an OpenAPI specification from YAML (JSON works too).
    let petstorePath = samplePath "petstore.yaml"

    let result = readSpecification petstorePath

    match result with
    | Result.Ok doc -> printfn "Loaded: %s (v%s)" doc.Info.Title doc.Info.Version
    | Result.Error err -> printfn "Error: %A" err

    result

// ---------------------------------------------------------------------------
// Section 2: Schema graph traversal
// ---------------------------------------------------------------------------

let demonstrateSchemaGraph (doc: OpenApiDocument) =
    printSection "2. Schema graph traversal"

    // Collect schema graph IR — all component schemas and their relationships.
    let graph = collectDocumentSchemas doc

    printfn "Schemas: %d nodes, %d edges" graph.Nodes.Count graph.Edges.Count

    for node in graph.Nodes do
        printfn "  %s (%A) %s" (schemaNodeLabel node) node.Kind (nullableSuffix node.Nullable)

// ---------------------------------------------------------------------------
// Section 3: Route map
// ---------------------------------------------------------------------------

let demonstrateRouteMap (doc: OpenApiDocument) =
    printSection "3. Route map"

    // Extract all API routes with their referenced schemas.
    let routes = collectRouteMap doc

    for route in routes.Routes do
        printfn "  %s %s → %d schemas" route.Method route.Path route.ResponseSchemas.Length

// ---------------------------------------------------------------------------
// Section 4: Links graph
// ---------------------------------------------------------------------------

let demonstrateLinksGraph (doc: OpenApiDocument) =
    printSection "4. Links graph"

    // Discover operation-to-operation links (petstore has none; see tests for richer examples).
    let links = collectLinksGraph doc

    printfn "Links: %d operations, %d links" links.Operations.Length links.Links.Length

// ---------------------------------------------------------------------------
// Section 5: Linting
// ---------------------------------------------------------------------------

let demonstrateLinting (doc: OpenApiDocument) =
    printSection "5. Linting"

    // Run all default lint rules.
    let lintResult = Linter.lintWithDefaults doc

    printfn "Violations: %d" lintResult.Violations.Length

    let previewCount = min 5 lintResult.Violations.Length

    for violation in lintResult.Violations |> List.take previewCount do
        printfn "  [%A] %s: %s" violation.Severity violation.Rule violation.Message

    // Configure: disable a specific rule.
    let config = LinterConfig.defaults |> LinterConfig.without [ "empty-parameter-description" ]
    let filtered = Linter.lintWithConfig config doc

    printfn "After disabling rule: %d violations" filtered.Violations.Length

// ---------------------------------------------------------------------------
// Section 6: Custom lint rule
// ---------------------------------------------------------------------------

let demonstrateCustomLintRule (doc: OpenApiDocument) =
    printSection "6. Custom lint rule"

    // Write a custom lint rule — flag GET operations that declare a request body.
    let noGetWithBody : LintRule =
        fun document ->
            Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters.allOperations document
            |> List.choose (fun (path, method, operation) ->
                match operation.RequestBody with
                | null -> None
                | _ when method = HttpMethod.Get ->
                    Some {
                        Rule = "custom-no-get-body"
                        Severity = Severity.Warning
                        Message = sprintf "GET %s has request body" path
                        Location = OperationLevel(path, method.Method, Option.ofObj operation.OperationId)
                    }
                | _ -> None)

    let withCustom = LinterConfig.defaults |> LinterConfig.withCustom [ noGetWithBody ]
    let customResult = Linter.lintWithConfig withCustom doc

    let customViolations =
        customResult.Violations |> List.filter (fun violation -> violation.Rule = "custom-no-get-body")

    printfn "Custom rule violations: %d" customViolations.Length

    for violation in customViolations do
        printfn "  [%A] %s" violation.Severity violation.Message

// ---------------------------------------------------------------------------
// Section 7: Scissors (subsetting)
// ---------------------------------------------------------------------------

let demonstrateScissors (doc: OpenApiDocument) =
    printSection "7. Scissors (subsetting)"

    // Cut a subset of the spec by tags; Transitive copies referenced component schemas.
    let options = { ScissorsOptions.Empty with IncludeTags = [ "pets" ]; Transitive = true }
    let cut = cutDocument doc options

    printfn "Cut spec: %d paths" cut.Paths.Count

// ---------------------------------------------------------------------------
// Section 8: Merge
// ---------------------------------------------------------------------------

let demonstrateMerge () =
    printSection "8. Merge"

    // Merge multiple specs (first-wins conflict resolution on paths and components).
    let mergeResult =
        mergeFiles [ samplePath "petstore.yaml"; samplePath "petstore-extended.yaml" ]

    match mergeResult with
    | Result.Ok merged -> printfn "Merged: %d paths" merged.Paths.Count
    | Result.Error err -> printfn "Merge error: %A" err

// ---------------------------------------------------------------------------
// Section 9: Visualization (requires Graphviz 'dot' in PATH)
// ---------------------------------------------------------------------------

let demonstrateVisualization (doc: OpenApiDocument) =
    printSection "9. Visualization"

    let outPath = Path.Combine(AppContext.BaseDirectory, "schema-graph.svg")

    // Export schema graph to SVG (requires Graphviz 'dot' in PATH).
    try
        exportSchemaGraphToSvg doc outPath
        printfn "Schema graph SVG written to %s" outPath
    with _ ->
        printfn "Graphviz not available, skipping SVG export"

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

[<EntryPoint>]
let main _argv =
    printfn "Functional.Microsoft.OpenAPI.Extensions — live examples"
    printfn "Sample files resolved from: %s" (Path.Combine(AppContext.BaseDirectory, "Samples"))

    match demonstrateReading () with
    | Result.Error err ->
        printfn "Cannot continue without a loaded document: %A" err
        1
    | Result.Ok doc ->
        demonstrateSchemaGraph doc
        demonstrateRouteMap doc
        demonstrateLinksGraph doc
        demonstrateLinting doc
        demonstrateCustomLintRule doc
        demonstrateScissors doc
        demonstrateMerge ()
        demonstrateVisualization doc
        0

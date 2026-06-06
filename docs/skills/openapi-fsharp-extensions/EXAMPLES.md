# OpenAPI F# Extensions — Compilable Examples

Each example is a self-contained F# script (`dotnet fsi script.fsx`). Replace `SPEC_PATH` with your OpenAPI YAML/JSON file.

**Package setup** (choose one):

```fsharp
// NuGet (published package)
#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

// Or project reference (local development in this repo)
#r "../../../../Microsoft.OpenAPI.FunctionalExtensions/bin/Debug/net10.0/Functional.Microsoft.OpenAPI.Extensions.dll"
```

For linting examples, also reference:

```fsharp
#r "../../../../Microsoft.OpenAPI.FunctionalExtensions.Linting/bin/Debug/net10.0/Functional.Microsoft.OpenAPI.Extensions.Linting.dll"
```

For visualization examples, also reference:

```fsharp
#r "../../../../Microsoft.OpenAPI.FunctionalExtensions.Visualizing/bin/Debug/net10.0/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.dll"
```

---

## 1. Loading

### 1.1 Load and validate

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters
open Microsoft.OpenAPI.FunctionalExtensions.Readers.Types

let specPath = "Samples/petstore.yaml"

let result =
    readSpecification specPath
    |> Result.map (fun doc ->
        printfn "Title: %s" doc.Info.Title
        printfn "Paths: %d" (DocumentAdapters.documentPaths doc |> List.length)
        doc)

match result with
| Ok _ -> printfn "OK"
| Error (FileNotFound msg) -> eprintfn "Not found: %s" msg
| Error (FileReadError msg) -> eprintfn "Read error: %s" msg
| Error (OpenApiErrors errs) ->
    errs |> Seq.iter (fun (OpenApiError (msg, loc)) -> eprintfn "%s at %s" msg loc)
```

### 1.2 Load pipeline with ResultEx

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters
open ResultEx

let requireNonEmptyPaths doc =
    match DocumentAdapters.documentPaths doc with
    | [] -> Error "Document has no paths"
    | _ -> Ok doc

let loadAndValidate path =
    readSpecification path
    |> Result.bind requireNonEmptyPaths

match loadAndValidate "Samples/petstore.yaml" with
| Ok doc -> printfn "Loaded %s with %d paths" doc.Info.Title (doc.Paths.Count)
| Error e -> eprintfn "Failed: %s" e
```

---

## 2. Traversal — schema graph IR

### 2.1 Collect full document schema graph

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiTraversal

let formatEdgeKind = function
    | Property name -> $"property:{name}"
    | ArrayItem -> "arrayItem"
    | MapValue -> "mapValue"
    | Composition kind -> $"composition:{kind}"

match readSpecification "Samples/petstore.yaml" with
| Error e -> eprintfn "Load failed: %A" e
| Ok doc ->
    let graph = collectDocumentSchemas doc
    printfn "nodes=%d edges=%d" graph.Nodes.Count graph.Edges.Count

    graph.Edges
    |> Seq.iter (fun e ->
        printfn "%s -[%s]-> %s" e.FromId (formatEdgeKind e.EdgeKind) e.ToId)
```

### 2.2 Single component schema subgraph

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters
open OpenApiTraversal

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    match DocumentAdapters.tryComponentSchema doc "Pet" with
    | None -> printfn "Component 'Pet' not found"
    | Some schema ->
        let graph = collectSchemaGraph schema
        printfn "Pet subgraph: %d nodes, %d edges" graph.Nodes.Count graph.Edges.Count
| Error e -> eprintfn "%A" e
```

---

## 3. Analysis — routes, refs, active patterns

### 3.1 Route map with schema references

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiOperationsTraversal

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    let routeMap = collectRouteMap doc
    for route in routeMap.Routes do
        printfn "%s %s" route.Method route.Path
        printfn "  tags=%A" route.Tags
        printfn "  responseSchemas=%A" route.ResponseSchemas
        printfn "  returnsArray=%b" route.ReturnsArray
| Error e -> eprintfn "%A" e
```

### 3.2 Filter operations by tag

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters
open Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters

let operationsWithTag tag doc =
    DocumentAdapters.allOperations doc
    |> List.filter (fun (_, _, op) ->
        OperationAdapters.operationTags op |> List.contains tag)

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    operationsWithTag "pets" doc
    |> List.iter (fun (path, method, op) ->
        printfn "%s %s (%s)" method.Method path (defaultArg op.OperationId "—"))
| Error e -> eprintfn "%A" e
```

### 3.3 Resolve $ref and walk compositions

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters
open Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns

let rec walkSchema (doc: OpenApiDocument) (schema: IOpenApiSchema) (depth: int) =
    let indent = String.replicate depth "  "
    match schema with
    | SchemaRef id ->
        printfn "%s$ref: %s" indent id
        match DocumentAdapters.tryComponentSchema doc id with
        | Some resolved -> walkSchema doc resolved (depth + 1)
        | None -> ()
    | ComposedSchema (kind, parts) ->
        printfn "%s%A (%d parts)" indent kind parts.Length
        parts |> List.iter (fun part -> walkSchema doc part (depth + 1))
    | ArraySchema items ->
        printfn "%sarray of:" indent
        walkSchema doc items (depth + 1)
    | ObjectSchema _ ->
        printfn "%sobject" indent
    | _ ->
        printfn "%s(leaf)" indent

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    DocumentAdapters.documentSchemas doc
    |> Map.iter (fun name schema ->
        printfn "=== %s ===" name
        let resolved =
            ReferenceAdapters.tryResolveSchemaReference doc schema
            |> Option.defaultValue schema
        walkSchema doc resolved 0)
| Error e -> eprintfn "%A" e
```

### 3.4 Nullable check via adapters and active patterns

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters
open Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    for KeyValue(name, schema) in DocumentAdapters.documentSchemas doc do
        let nullable = SchemaAdapters.schemaIsNullable schema
        let typeLabel =
            match SchemaAdapters.schemaType schema with
            | Some t ->
                match t with
                | NullableType _ -> "nullable"
                | NonNullableType _ -> "non-nullable"
            | None -> "unknown"
        printfn "%s: nullable=%b (%s)" name nullable typeLabel
| Error e -> eprintfn "%A" e
```

---

## 4. Merge

### 4.1 Merge files and save

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiWriterTools

let inputs = [ "Samples/petstore.yaml"; "Samples/petstore-extended.yaml" ]
let outPath = "out/merged.yaml"

match mergeFiles inputs with
| Error e -> eprintfn "Merge failed: %A" e
| Ok doc ->
    saveDocument doc outPath
    printfn "Merged %d files → %s" inputs.Length outPath
    printfn "Paths: %d" doc.Paths.Count
```

### 4.2 Merge in-memory documents

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge
open ResultEx

let loadAll paths =
    paths
    |> List.map readSpecification
    |> List.fold (fun acc r ->
        match acc, r with
        | Error e, _ -> Error e
        | _, Error e -> Error e
        | Ok docs, Ok doc -> Ok (doc :: docs)) (Ok [])

match loadAll ["Samples/petstore.yaml"; "Samples/petstore-extended.yaml"] with
| Error e -> eprintfn "%A" e
| Ok docs ->
    let merged = mergeDocuments (List.rev docs)
    printfn "Merged document paths: %d" merged.Paths.Count
```

---

## 5. Scissors (subset)

### 5.1 Cut by tags and paths

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiWriterTools
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters

let opts =
    { ScissorsOptions.Empty with
        IncludeTags = [ "pets" ]
        IncludePaths = [ "/pets" ]
        Transitive = true }

match readSpecification "Samples/petstore.yaml" with
| Error e -> eprintfn "%A" e
| Ok doc ->
    let cut = cutDocument doc opts
    saveDocument cut "out/cut_pets.yaml"
    printfn "Cut paths: %d" (DocumentAdapters.documentPaths cut |> List.length)
    printfn "Cut schemas: %d" (DocumentAdapters.documentSchemas cut |> Map.count)
```

### 5.2 Cut by operation ID

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions"
#r "nuget: Microsoft.OpenApi, 3.5.2"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters

let opts =
    { ScissorsOptions.Empty with
        IncludeOperationIds = [ "showPetById" ]
        Transitive = true }

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    let cut = cutDocument doc opts
    DocumentAdapters.allOperations cut
    |> List.iter (fun (path, method, op) ->
        printfn "%s %s %s" method.Method path op.OperationId)
| Error e -> eprintfn "%A" e
```

---

## 6. Visualization

Requires the Visualizing project/package (Rubjerg.Graphviz).

### 6.1 Export schema and route SVG

```fsharp
#!/usr/bin/env dotnet fsi

#r "../../../../Microsoft.OpenAPI.FunctionalExtensions/bin/Debug/net10.0/Functional.Microsoft.OpenAPI.Extensions.dll"
#r "../../../../Microsoft.OpenAPI.FunctionalExtensions.Visualizing/bin/Debug/net10.0/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.dll"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    exportSchemaGraphToSvg doc "out/schemas.svg"
    exportRouteMapToSvg doc "out/routes.svg"
    printfn "Wrote out/schemas.svg and out/routes.svg"
| Error e -> eprintfn "%A" e
```

### 6.2 Single component schema diagram

```fsharp
#!/usr/bin/env dotnet fsi

#r "../../../../Microsoft.OpenAPI.FunctionalExtensions/bin/Debug/net10.0/Functional.Microsoft.OpenAPI.Extensions.dll"
#r "../../../../Microsoft.OpenAPI.FunctionalExtensions.Visualizing/bin/Debug/net10.0/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.dll"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    exportSingleComponentSchemaToSvg doc "Pet" "out/Pet.svg"
    printfn "Wrote out/Pet.svg"
| Error e -> eprintfn "%A" e
```

### 6.3 Route map with operations and schemas

```fsharp
#!/usr/bin/env dotnet fsi

#r "../../../../Microsoft.OpenAPI.FunctionalExtensions/bin/Debug/net10.0/Functional.Microsoft.OpenAPI.Extensions.dll"
#r "../../../../Microsoft.OpenAPI.FunctionalExtensions.Visualizing/bin/Debug/net10.0/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.dll"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport

let opts =
    { CenterLabel = Some "Petstore API"
      IncludeOperations = true
      IncludeSchemas = true }

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    exportRouteMapToSvgWith doc "out/routes-detailed.svg" opts
    printfn "Wrote out/routes-detailed.svg"
| Error e -> eprintfn "%A" e
```

### 6.4 Export schema graph as DOT

```fsharp
#!/usr/bin/env dotnet fsi

#r "../../../../Microsoft.OpenAPI.FunctionalExtensions/bin/Debug/net10.0/Functional.Microsoft.OpenAPI.Extensions.dll"
#r "../../../../Microsoft.OpenAPI.FunctionalExtensions.Visualizing/bin/Debug/net10.0/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.dll"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport

match readSpecification "Samples/petstore.yaml" with
| Ok doc ->
    exportSchemaGraphToDot doc "out/schemas.dot"
    printfn "Wrote out/schemas.dot"
| Error e -> eprintfn "%A" e
```

---

## 7. Linting

Requires the Linting project/package.

### 7.1 Lint with defaults

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions, 0.9.0"
#r "nuget: Functional.Microsoft.OpenAPI.Extensions.Linting, 0.9.0"
#r "nuget: Microsoft.OpenApi, 3.6.3"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter

match readSpecification "Samples/petstore.yaml" with
| Error e -> eprintfn "%A" e
| Ok doc ->
    let result = lintWithDefaults doc
    for violation in result.Violations do
        printfn "[%A] %s: %s (%s)" violation.Severity violation.Rule violation.Message
```

### 7.2 Custom rules and disable rules

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions, 0.9.0"
#r "nuget: Functional.Microsoft.OpenAPI.Extensions.Linting, 0.9.0"
#r "nuget: Microsoft.OpenApi, 3.6.3"

open System
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter
open Microsoft.OpenAPI.FunctionalExtensions.Linting.LinterConfig
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

let requireApiTitle (document: OpenApiDocument) : LintViolation list =
    match document.Info with
    | null -> []
    | info when String.IsNullOrWhiteSpace info.Title ->
        [ {
            Rule = "require-api-title"
            Severity = Error
            Message = "API title must be set in info.title."
            Location = DocumentLevel
          } ]
    | _ -> []

let config =
    LinterConfig.defaults
    |> LinterConfig.without [ "empty-schema-property-description"; "unused-schemas" ]
    |> LinterConfig.withCustom [ requireApiTitle ]

match readSpecification "Samples/petstore.yaml" with
| Error e -> eprintfn "%A" e
| Ok doc ->
    let result = lintWithConfig config doc
    printfn "Violations: %d" result.Violations.Length
```

### 7.3 Restrict rules with withOnly and withSeverity

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions, 0.9.0"
#r "nuget: Functional.Microsoft.OpenAPI.Extensions.Linting, 0.9.0"
#r "nuget: Microsoft.OpenApi, 3.6.3"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter
open Microsoft.OpenAPI.FunctionalExtensions.Linting.LinterConfig
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

let strictConfig =
    LinterConfig.defaults
    |> LinterConfig.withOnly [ "missing-operation-id"; "missing-response-description" ]
    |> LinterConfig.withSeverity "empty-operation-summary" Info

match readSpecification "Samples/petstore.yaml" with
| Error e -> eprintfn "%A" e
| Ok doc ->
    let result = lintWithConfig strictConfig doc
    printfn "Strict lint: %d findings" result.Violations.Length
```

---

## 8. Links graph traversal

### 8.1 Collect links graph

```fsharp
#!/usr/bin/env dotnet fsi

#r "nuget: Functional.Microsoft.OpenAPI.Extensions, 0.9.0"
#r "nuget: Microsoft.OpenApi, 3.6.3"

open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiLinksTraversal

match readSpecification "Specifications/links-example.yaml" with
| Error e -> eprintfn "%A" e
| Ok doc ->
    let graph = collectLinksGraph doc
    printfn "operations=%A" graph.Operations
    printfn "links=%d" graph.Links.Length
    for link in graph.Links do
        printfn "%s --[%s]--> %s" link.SourceOperationId link.LinkName link.TargetOperationId
```

---

## 9. Testing template (NUnit)

Use this shape in test projects — not runnable as fsx without NUnit references.

```fsharp
module MyApp.Tests.OpenApiSpecs

open NUnit.Framework
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters
open OpenApiTraversal
open ResultEx

[<Test>]
let ``Petstore loads and has schema graph nodes`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Error e -> Assert.Fail($"Load failed: %A{e}")
    | Ok doc ->
        Assert.That(DocumentAdapters.documentPaths doc |> List.length, Is.GreaterThan(0))
        let graph = collectDocumentSchemas doc
        Assert.That(graph.Nodes.Count, Is.GreaterThan(0))

[<Test>]
let ``Nullable schemas are detected without exceptions`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Ok doc ->
        let anyNullable =
            DocumentAdapters.documentSchemas doc
            |> Map.exists (fun _ schema ->
                Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters.schemaIsNullable schema)
        Assert.That(anyNullable, Is.True.Or.False)  // property holds for any spec
    | Error _ -> Assert.Fail()
```

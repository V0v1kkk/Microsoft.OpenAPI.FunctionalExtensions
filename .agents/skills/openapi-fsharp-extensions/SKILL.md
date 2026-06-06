---
name: openapi-fsharp-extensions
description: >
  Guide for using the Functional.Microsoft.OpenAPI.Extensions F# library to
  load, traverse, analyze, merge, subset, and visualize OpenAPI specifications
  on top of Microsoft.OpenApi. Use when working with OpenAPI specs in F#,
  building schema graph or route map IR, resolving $ref references, filtering
  operations by tag, merging multiple specs, cutting subsets with scissors, or
  exporting Graphviz DOT/SVG diagrams.
version: '1.0'
tags: [openapi, fsharp, microsoft-openapi, graphviz, traversal, merge]
metadata:
  author: Vladimir Rogozhin
  packages:
    core: Functional.Microsoft.OpenAPI.Extensions
    visualizing: Microsoft.OpenAPI.FunctionalExtensions.Visualizing
  targetFramework: net9.0
  openApiVersion: Microsoft.OpenApi 3.5.2
requiresDependencies:
  - "Functional.Microsoft.OpenAPI.Extensions (NuGet)"
  - "Microsoft.OpenAPI.FunctionalExtensions.Visualizing (project/NuGet, for Graphviz export)"
  - "Microsoft.OpenApi + Microsoft.OpenApi.YamlReader (transitive via core)"
  - "Rubjerg.Graphviz (transitive via Visualizing)"
---

# OpenAPI F# Functional Extensions

## Purpose & When to Use

Load this skill when the user wants to:

- Work with OpenAPI (Swagger) specs in **F#** using a functional, null-safe API
- **Traverse** schema graphs (`allOf`/`oneOf`/`anyOf`, properties, arrays, maps)
- **Extract route maps** (paths, methods, tags, schema references per operation)
- **Merge** multiple OpenAPI documents into one
- **Subset** a spec by tags, paths, or operation IDs (scissors)
- **Visualize** schemas or routes as Graphviz DOT/SVG
- **Resolve** `$ref` references and inspect nullability via `JsonSchemaType` flags

Do **not** use raw `Microsoft.OpenApi` null checks in consumer code — prefer the adapters and `Result`-based loaders from this library.

---

## Installation

### Core library (required)

```bash
dotnet add package Functional.Microsoft.OpenAPI.Extensions
```

Or project reference:

```xml
<ProjectReference Include="path/to/Microsoft.OpenAPI.FunctionalExtensions.fsproj" />
```

### Visualization (optional)

Reference the `Microsoft.OpenAPI.FunctionalExtensions.Visualizing` project, or add it when published:

```xml
<ProjectReference Include="path/to/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.fsproj" />
```

### CLI tool (optional)

```bash
dotnet run --project Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool -- --help
```

Requires .NET 9 SDK.

---

## Core Concepts

| Concept | Description |
|---------|-------------|
| **Adapters** | Null-safe wrappers over `IOpenApi*` interfaces; return `option`, `Map`, `Set`, `list` instead of null collections |
| **Result pipeline** | `readSpecification` returns `Result<OpenApiDocument, ReaderError>` — compose with `Result.bind` / `ResultEx` |
| **Active patterns** | `SchemaRef`, `ArraySchema`, `ObjectSchema`, `ComposedSchema` decompose schemas in `match` expressions |
| **Schema Graph IR** | `SchemaGraph` with `SchemaNode` (id, title, kind, format, enum…) and `SchemaEdge` (Property, ArrayItem, MapValue, Composition) |
| **Route Map IR** | `RouteMap` with `Route` records (path, method, tags, schema ref pointers, array-return flags) |
| **IR vs rendering** | Traversal modules produce renderer-agnostic IR; `GraphvizExport` consumes IR only |
| **OpenAPI.NET specifics** | HTTP methods are `System.Net.Http.HttpMethod`; nullability is `JsonSchemaType.Null` flag, not a `Nullable` bool |

---

## Quick Start Examples

### Load a spec file

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools

let loadSpec path =
    match readSpecification path with
    | Ok doc -> printfn "Loaded: %s" doc.Info.Title; doc
    | Error err -> failwithf "Failed: %A" err
```

### List all component schemas

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters

let listSchemas (doc: Microsoft.OpenApi.OpenApiDocument) =
    DocumentAdapters.documentSchemas doc
    |> Map.keys
    |> Seq.iter (printfn "schema: %s")
```

### Traverse schema graph

```fsharp
open OpenApiTraversal

let graphFromDoc doc =
    let g = collectDocumentSchemas doc
    printfn "nodes=%d edges=%d" g.Nodes.Count g.Edges.Count
    g.Edges
    |> Seq.iter (fun e ->
        let kind =
            match e.EdgeKind with
            | Property n -> $"property:{n}"
            | ArrayItem -> "arrayItem"
            | MapValue -> "mapValue"
            | Composition ck -> $"composition:{ck}"
        printfn "%s -[%s]-> %s" e.FromId kind e.ToId)
```

### Collect route map

```fsharp
open OpenApiOperationsTraversal

let printRoutes doc =
    let m = collectRouteMap doc
    for r in m.Routes do
        printfn "%s %s tags=%A" r.Method r.Path r.Tags
```

### Merge multiple specs

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiWriterTools

let mergeAndSave inputs outPath =
    match mergeFiles inputs with
    | Error e -> Error e
    | Ok doc ->
        saveDocument doc outPath
        Ok doc
```

### Cut spec by tags/paths

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiWriterTools

let cutPets doc outPath =
    let opts =
        { ScissorsOptions.Empty with
            IncludeTags = [ "pets" ]
            IncludePaths = [ "/pets" ]
            Transitive = true }
    let cut = cutDocument doc opts
    saveDocument cut outPath
```

### Export to DOT/SVG

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport

let visualize path svgOut =
    match readSpecification path with
    | Ok doc ->
        exportSchemaGraphToSvg doc (svgOut.Replace(".svg", "-schemas.svg"))
        exportRouteMapToSvg doc (svgOut.Replace(".svg", "-routes.svg"))
        Ok ()
    | Error e -> Error e
```

---

## API Reference

Namespaces use `Microsoft.OpenAPI.FunctionalExtensions.*` unless noted.

### AdapterCore

`ofObj`, `ofNullable`, `readMap`, `readSeq`, `readSet` — null-safe conversions to `option`/`Map`/`list`/`Set`.

### ActivePatterns (`ActivePatterns` module)

| Pattern | Matches |
|---------|---------|
| `SchemaRef` | Component reference id from schema |
| `ArraySchema` | Array schema → item schema |
| `ObjectSchema` | Object with properties → `IDictionary<string, IOpenApiSchema>` |
| `ComposedSchema` | `allOf`/`oneOf`/`anyOf` → `(CompositionKind * IOpenApiSchema list)` |
| `NotNull` | Any reference type that is not null |
| `NullableType` / `NonNullableType` | `JsonSchemaType` with/without `Null` flag |

### SchemaAdapters

`schemaTitle`, `schemaDescription`, `schemaType`, `schemaFormat`, `schemaIsNullable` (via `JsonSchemaType.Null` flag), `schemaProperties`, `schemaRequired`, `schemaItems`, `schemaAllOf`/`schemaOneOf`/`schemaAnyOf`, `schemaNot`, `schemaAdditionalProperties`, `schemaEnum`, `schemaReadOnly`/`schemaWriteOnly`/`schemaDeprecated`.

### ReferenceAdapters

| Function | Purpose |
|----------|---------|
| `trySchemaReferenceId` | Extract component name from `$ref` |
| `referencePointer` | `id` → `#/components/schemas/{id}` |
| `tryResolveSchemaReference` | Resolve ref against `doc.Components.Schemas` |
| `isUnresolvedReference` | True for unresolved `OpenApiSchemaReference` |

### OperationAdapters

| Function | Returns |
|----------|---------|
| `pathItemOperations` | `(HttpMethod * OpenApiOperation) list` |
| `operationTags` | `string list` |
| `operationParameters` | `IOpenApiParameter list` |
| `operationRequestBody` | `IOpenApiRequestBody option` |
| `operationResponses` | `Map<string, IOpenApiResponse>` |
| `mediaTypeSchema` | `IOpenApiSchema option` |
| `schemasFromContent` | `IOpenApiSchema list` |
| `schemasFromOperation` | All schemas from params, request, responses |

### DocumentAdapters

`documentComponents`, `documentSchemas`, `documentResponses`/`documentParameters`/`documentRequestBodies`/`documentHeaders`, `documentServers`, `documentTags`, `documentPaths`, `foldAllOperations`, `allOperations`, `tryComponentSchema`.

### ExtensionAdapters

| Function | Purpose |
|----------|---------|
| `extensions` | `Map<string, IOpenApiExtension>` from dictionary |
| `tryExtensionJsonNode` | Read extension as `JsonNode` |
| `tryExtensionString` | Read extension as string |
| `extensionIsTruthy` | True when extension value is `"true"`, `"1"`, or `"yes"` |

Also: `OpenApiTools.getExtensionValue` and `OpenApiTools.tryGetValue` for recursive JsonNode extraction.

### OpenApiAdapters (aggregate helpers)

Prefer typed adapters for new code. Provides `componentsSchemas`, `tryGetComponentSchema`, `trySchemaRefName`, `schemaChildren`, `schemasFromContent`/`schemasFromOperation` (seq variants), `foldPaths`/`foldOperations`.

### OpenApiReaderTools

| Function | Returns |
|----------|---------|
| `readSpecification` | `string -> Result<OpenApiDocument, ReaderError>` |

Pipeline: validate path → read text → `OpenApiDocument.Parse` with YAML reader.

Error types (`Readers.Types`): `FileNotFound`, `FileReadError`, `OpenApiErrors`.

### OpenApiTraversal

| Type / Function | Purpose |
|-----------------|---------|
| `SchemaNode`, `SchemaEdge`, `SchemaGraph` | IR types |
| `SchemaEdgeKind` | `Property`, `ArrayItem`, `MapValue`, `Composition` |
| `collectSchemaGraph` | Graph from single root schema |
| `collectSchemaGraphWithRoot` | Graph with custom JSON pointer root |
| `collectDocumentSchemas` | Union of all `#/components/schemas/*` graphs |

Uses visited-cache (`HashSet`) to handle cycles.

### OpenApiOperationsTraversal

| Type / Function | Purpose |
|-----------------|---------|
| `Route`, `RouteMap` | IR types |
| `collectRouteMap` | All operations → routes with schema ref pointers |

Route fields: `ParameterSchemas`, `RequestSchemas`, `ResponseSchemas`, `ReturnsArray`, `ReturnsArrayViaData`.

### OpenApiMerge

| Function | Returns |
|----------|---------|
| `mergeFiles` | `string list -> Result<OpenApiDocument, MergeError>` |
| `mergeDocuments` | `OpenApiDocument list -> OpenApiDocument` |

Merge is **naive**: first-wins on key conflicts; paths and all component dictionaries merged.

### OpenApiScissors

| Type / Function | Purpose |
|-----------------|---------|
| `ScissorsOptions` | `IncludeTags`, `IncludePaths`, `IncludeOperationIds`, `Transitive` |
| `ScissorsOptions.Empty` | Default (`Transitive = true`) |
| `cutDocument` | Filter paths/operations; copy transitive component schemas |

Filter logic: operation kept if **any** of tag, path substring, or operationId matches (when filters specified).

### OpenApiWriterTools

| Function | Purpose |
|----------|---------|
| `saveDocument` | Write `.json` or `.yaml`/`.yml` via `SerializeAsV3` |

### GraphvizExport (`Visualizing` project)

| Function | Output |
|----------|--------|
| `exportSchemaGraphToSvg` | Full document schema graph → SVG |
| `exportSchemaGraphToDot` | Full document schema graph → DOT |
| `exportSingleComponentSchemaToSvg` | Single `#/components/schemas/{name}` → SVG |
| `exportRouteMapToSvg` | Hub + route chains (title as hub) |
| `exportRouteMapToSvgWith` | Custom `RouteSvgOptions` |

`RouteSvgOptions`: `CenterLabel`, `IncludeOperations`, `IncludeSchemas`.

---

## CLI Tool Usage

Program: `openapi-visualizer` (`Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool`)

| Command | Purpose |
|---------|---------|
| `--schema-svg` | Schema graph → SVG (or DOT with `--dot`) |
| `--route-svg` | Route map → SVG |
| `--schema-collect` | Schema Graph IR → JSON |
| `--route-collect` | Route Map IR → JSON |
| `--merge` | Merge specs → YAML/JSON |
| `--scissors` | Cut subset → YAML/JSON |

### Common flags

```bash
# Schema SVG
dotnet run --project Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool -- \
  --schema-svg --input Samples/petstore.yaml --out out/schema.svg

# Single component
dotnet run --project ... -- \
  --schema-svg --input spec.yaml --out out/Pet.svg --component Pet

# Schema DOT
dotnet run --project ... -- \
  --schema-svg --input spec.yaml --out out/schema.dot --dot

# Route SVG with options
dotnet run --project ... -- \
  --route-svg --input spec.yaml --out out/routes.svg \
  --center "My API" --include-operations --include-schemas

# Collect IR JSON
dotnet run --project ... -- --schema-collect --input spec.yaml --out out/schema.json
dotnet run --project ... -- --route-collect --input spec.yaml --out out/routes.json

# Merge
dotnet run --project ... -- \
  --merge --input a.yaml --input b.yaml --out out/merged.yaml

# Scissors
dotnet run --project ... -- \
  --scissors --input spec.yaml --out out/cut.yaml \
  --include-tag pets --include-path /pets --include-operation showPetById
```

Exit codes: `0` success, `2` input/parse error.

---

## Common Patterns

### Filter operations by tag

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters
open Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters

let operationsWithTag tag doc =
    DocumentAdapters.allOperations doc
    |> List.filter (fun (_, _, op) ->
        OperationAdapters.operationTags op |> List.contains tag)
```

### Find schemas referenced by an operation

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters
open Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters

let schemaRefsFromOperation doc op =
    OperationAdapters.schemasFromOperation op
    |> List.choose ReferenceAdapters.trySchemaReferenceId
    |> List.distinct
```

### Check if a schema is nullable

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters

let isNullable schema = SchemaAdapters.schemaIsNullable schema
// Or via active pattern:
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns

let hasNullType schemaType =
    match schemaType with
    | NullableType _ -> true
    | NonNullableType _ -> false
```

### Resolve $ref references

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters

let resolve doc schema =
    ReferenceAdapters.tryResolveSchemaReference doc schema
    |> Option.defaultValue schema
```

### Handle compositions (allOf/oneOf/anyOf)

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns
open Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters

let rec walkCompositions schema =
    match schema with
    | ComposedSchema (kind, parts) ->
        printfn "composition: %A" kind
        parts |> List.iter walkCompositions
    | _ ->
        SchemaAdapters.schemaAllOf schema |> List.iter walkCompositions
        SchemaAdapters.schemaOneOf schema |> List.iter walkCompositions
        SchemaAdapters.schemaAnyOf schema |> List.iter walkCompositions
```

For full graph traversal with edge kinds, use `collectSchemaGraph` / `collectDocumentSchemas`.

---

## Architecture Notes for AI Agents

### Module dependency graph

```
AdapterCore, StringExtensions, SeqExtensions, ResultEx
    ↓
OpenApiSchemaAnalysis, OpenApiTools, OpenApiReaderTypes
    ↓
OpenApiActivePatterns, SchemaAdapters, ReferenceAdapters,
OperationAdapters, DocumentAdapters, ExtensionAdapters
    ↓
OpenApiAdapters (aggregate)
    ↓
OpenApiTraversal, OpenApiOperationsTraversal  ← IR layer (renderer-agnostic)
    ↓
OpenApiMerge, OpenApiScissors, OpenApiWriterTools
    ↓
GraphvizExport (Visualizing project)  ← rendering only
```

### Where to add new functionality

| Need | Add to |
|------|--------|
| New null-safe property accessor | `*Adapters.fs` module |
| New schema decomposition pattern | `OpenApiActivePatterns.fs` |
| New IR field or edge kind | `OpenApiTraversal.fs` or `OpenApiOperationsTraversal.fs` |
| New diagram styling/labels | `GraphvizExport.fs` (never in traversal) |
| New CLI command | `Visualizing.Tool/Program.fs` |
| New merge/scissors behavior | `OpenApiMerge.fs` / `OpenApiScissors.fs` |

**Rules**: ROP (`Result`) for public workflows; work against `IOpenApi*` interfaces; IR must stay renderer-agnostic; handle null collections at edges.

### Testing conventions

- Framework: **NUnit 4**; property tests with **FsCheck**; snapshots with **Verify.NUnit**
- Tests live under `Tests/Microsoft.OpenAPI.FunctionalExtensions.Tests/`
- Cover: nullable handling, `$ref`, arrays, compositions, cycles, large specs
- CLI integration: `dotnet build` then `dotnet run --no-build` against `Samples/`
- Before PR: `dotnet build` + `dotnet test` on solution must be green

### Related skills in this repo

- [`fsharp-style`](../fsharp-style/SKILL.md) — F# coding conventions
- [`fsharp-active-patterns`](../fsharp-active-patterns/SKILL.md) — active pattern design
- Project rules: `.cursor/rules/project-rule.mdc`

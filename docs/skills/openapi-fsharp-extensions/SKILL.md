---
name: openapi-fsharp-extensions
description: >
  Guide for consuming the Functional.Microsoft.OpenAPI.Extensions F# library
  to load, traverse, analyze, merge, subset, and visualize OpenAPI specs on
  Microsoft.OpenApi v2. Use when working with OpenAPI/Swagger in F#, building
  schema graph or route map IR, resolving $ref references, filtering operations
  by tag, merging specs, cutting subsets with scissors, or exporting Graphviz
  DOT/SVG diagrams.
metadata:
  author: Vladimir Rogozhin
  packages:
    core: Functional.Microsoft.OpenAPI.Extensions
    visualizing: Microsoft.OpenAPI.FunctionalExtensions.Visualizing
  targetFramework: net9.0
  openApiVersion: Microsoft.OpenApi 3.5.2
---

# OpenAPI F# Functional Extensions (Consumer Skill)

## When to use

Load this skill when the user wants to **consume** the NuGet/project packages built from this repo — not when modifying the library itself.

| Task | Start here |
|------|------------|
| Load YAML/JSON OpenAPI file | `OpenApiReaderTools.readSpecification` |
| Null-safe property access on `IOpenApi*` | `*Adapters` modules |
| Pattern-match schema structure | `ActivePatterns` |
| Build schema relationship graph (IR) | `OpenApiTraversal` |
| List routes + schema refs per operation | `OpenApiOperationsTraversal` |
| Combine multiple spec files | `OpenApiMerge` |
| Cut spec by tags/paths/operationIds | `OpenApiScissors` |
| Export DOT/SVG diagrams | `GraphvizExport` (Visualizing project) |

Do **not** use raw `Microsoft.OpenApi` null checks in consumer code — use adapters and `Result`-based loaders.

## Installation

```bash
dotnet add package Functional.Microsoft.OpenAPI.Extensions
```

Visualization (optional):

```xml
<ProjectReference Include="path/to/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.fsproj" />
```

Requires .NET 9 SDK. OpenAPI.NET v2 specifics: HTTP methods are `System.Net.Http.HttpMethod`; nullability is `JsonSchemaType.Null` flag, not a `Nullable` bool.

---

## Decision tree: which module?

```
Need to work with an OpenAPI spec?
│
├─ Load from disk?
│   └─ YES → OpenApiReaderTools.readSpecification
│            Returns Result<OpenApiDocument, ReaderError>
│
├─ Read a single property safely (title, type, paths, tags)?
│   ├─ On IOpenApiSchema?        → SchemaAdapters
│   ├─ On OpenApiOperation?      → OperationAdapters
│   ├─ On OpenApiDocument?       → DocumentAdapters
│   ├─ On $ref / components?     → ReferenceAdapters
│   ├─ On x-* extensions?        → ExtensionAdapters + OpenApiTools
│   └─ Aggregate traversal?      → OpenApiAdapters (foldPaths, schemaChildren)
│
├─ Pattern-match schema shape in match expressions?
│   └─ ActivePatterns (SchemaRef, ArraySchema, ObjectSchema, ComposedSchema)
│
├─ Build renderer-agnostic IR?
│   ├─ Schema nodes + edges (allOf/oneOf/anyOf, properties, arrays)?
│   │   └─ OpenApiTraversal.collectDocumentSchemas / collectSchemaGraph
│   └─ Routes with schema ref pointers per operation?
│       └─ OpenApiOperationsTraversal.collectRouteMap
│
├─ Transform the document?
│   ├─ Merge multiple files?     → OpenApiMerge.mergeFiles
│   ├─ Subset by tag/path/opId?  → OpenApiScissors.cutDocument
│   └─ Save result?              → OpenApiWriterTools.saveDocument
│
└─ Visualize IR as DOT/SVG?
    └─ GraphvizExport (separate Visualizing package — never put rendering in traversal)
```

---

## Core mental model

| Layer | Role | Key types |
|-------|------|-----------|
| **Adapters** | Null-safe reads over `IOpenApi*` → `option`, `Map`, `list`, `Set` | — |
| **Active patterns** | Declarative schema decomposition in `match` | `CompositionKind` (in ActivePatterns) |
| **IR (traversal)** | Renderer-agnostic graphs | `SchemaGraph`, `RouteMap` |
| **Transform** | Merge, subset, write | `ScissorsOptions`, `MergeError` |
| **Visualize** | IR → Graphviz only | `RouteSvgOptions` |

**ROP is mandatory**: public workflows return `Result<_,_>`. Compose with `Result.bind` / `ResultEx` helpers — never `failwith` for expected failures.

---

## Module dependency order

Open source files in this order when exploring or extending consumer code:

```
StringExtensions, SeqExtensions, Results, ResultEx
    ↓
AdapterCore
    ↓
OpenApiTools, OpenApiReaderTypes, OpenApiSchemaAnalysis
    ↓
OpenApiReaderTools
    ↓
OpenApiActivePatterns, SchemaAdapters, ReferenceAdapters,
OperationAdapters, DocumentAdapters, ExtensionAdapters
    ↓
OpenApiAdapters (aggregate)
    ↓
OpenApiTraversal, OpenApiOperationsTraversal   ← IR (renderer-agnostic)
    ↓
OpenApiMerge, OpenApiScissors, OpenApiWriterTools
    ↓
GraphvizExport (Visualizing project)           ← rendering only
```

---

## Common patterns: before / after

### Reading properties (anti-pattern → preferred)

**Without adapters** (fragile — null collections, exceptions):

```fsharp
// BAD: null checks everywhere, OperationType is gone in v2
let tags = op.Tags |> Seq.map (fun t -> t.Name) |> Seq.toList  // NRE if Tags is null
```

**With adapters** (total at the edge):

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters

let tags = OperationAdapters.operationTags op  // string list, never null
```

### Traversing all operations

**Without adapters**:

```fsharp
for kv in doc.Paths do
    for opKv in kv.Value.Operations do ...  // crashes on null Paths/Operations
```

**With adapters**:

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters

DocumentAdapters.allOperations doc
|> List.iter (fun (path, method, op) -> ...)
```

### Schema decomposition

**Without adapters** (imperative null checks):

```fsharp
if schema.AllOf <> null && schema.AllOf.Count > 0 then ...
elif schema.OneOf <> null && ...
```

**With active patterns**:

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns

match schema with
| ComposedSchema (AllOf, parts) -> ...
| ArraySchema items -> ...
| ObjectSchema props -> ...
| SchemaRef id -> ...
| _ -> ()
```

---

## Error handling patterns

### Load pipeline

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Readers.Types
open ResultEx

let loadSpec path =
    readSpecification path
    |> Result.bind (fun doc ->
        if DocumentAdapters.documentPaths doc |> List.isEmpty
        then Error (FileReadError "No paths in document")
        else Ok doc)
```

`ReaderError` cases: `FileNotFound`, `FileReadError`, `OpenApiErrors` (seq of `OpenApiError` with message + JSON pointer).

### Merge pipeline

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge

mergeFiles ["a.yaml"; "b.yaml"]
|> Result.map (fun doc -> cutDocument doc ScissorsOptions.Empty)
```

`MergeError`: `FileNotFound`, `ParseError`, `MergeConflict`. Merge is **naive first-wins** on key conflicts.

### Propagate, don't swallow

```fsharp
// BAD
match readSpecification path with
| Ok d -> process d
| Error _ -> ()   // silent failure

// GOOD
readSpecification path |> Result.bind process
```

Use `ResultEx.teeError` for logging at boundaries; keep domain logic in `bind` chains.

---

## Common mistakes and anti-patterns

| Mistake | Why it fails | Fix |
|---------|--------------|-----|
| `schema.Nullable` bool | Removed in OpenAPI.NET v2 | `SchemaAdapters.schemaIsNullable` or `NullableType` active pattern |
| `OperationType` enum | Replaced by `HttpMethod` | `OperationAdapters.pathItemOperations` |
| Direct `doc.Paths` iteration | `Paths` may be null | `DocumentAdapters.documentPaths` / `allOperations` |
| Assuming `mergeDocuments` resolves conflicts | First-wins only | Document merge limitations; validate output |
| Putting Graphviz labels in traversal | Violates separation of concerns | IR in `OpenApiTraversal`; labels in `GraphvizExport` |
| `failwith` on `readSpecification` errors | Breaks ROP | Return `Result` or `bind` |
| Casting `IOpenApiSchema` to `OpenApiSchema` | References are wrappers | Pattern-match concrete types only when necessary; prefer adapters |
| Ignoring cycles in manual recursion | Infinite loops | Use `collectSchemaGraph` (has visited cache) |

---

## Testing patterns

When writing tests that use this library:

1. **Load fixtures** via `readSpecification "Specifications/petstore.yaml"` — test projects copy specs to output dir.
2. **Assert on `Result`** — never assume load succeeds without matching `Ok`/`Error`.
3. **Adapter tests** — verify null-safe behavior: empty maps/lists, not exceptions.
4. **IR tests** — `collectDocumentSchemas` / `collectRouteMap` node and edge counts; use **Verify.NUnit** snapshots for stable DOT/JSON IR output.
5. **Property tests** — FsCheck for nullable handling, ref resolution, composition edge cases.
6. **CLI integration** — `dotnet build` then `dotnet run --no-build` on Visualizing.Tool against `Samples/`.

Framework: NUnit 4. Helpers: `ResultEx`, `VerifyNUnit`.

See [EXAMPLES.md](EXAMPLES.md) for compilable scripts and [REFERENCE.md](REFERENCE.md) for full API signatures.

---

## CLI tool (optional)

Program: `openapi-visualizer` (`Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool`)

| Flag | Purpose |
|------|---------|
| `--schema-svg` | Schema graph → SVG (add `--dot` for DOT) |
| `--route-svg` | Route map → SVG |
| `--schema-collect` / `--route-collect` | IR → JSON |
| `--merge` | Merge specs |
| `--scissors` | Cut subset (`--include-tag`, `--include-path`, `--include-operation`) |

Exit codes: `0` success, `2` input/parse error.

---

## Related skills

- [`fsharp-style`](../../.agents/skills/fsharp-style/SKILL.md) — F# coding conventions (if symlinked in repo)
- [`fsharp-active-patterns`](../../.agents/skills/fsharp-active-patterns/SKILL.md) — active pattern design
- Project rules: `.cursor/rules/project-rule.mdc` (when working inside this repo)

## Additional resources

- **Compilable examples by use case**: [EXAMPLES.md](EXAMPLES.md)
- **Module-by-module API tables**: [REFERENCE.md](REFERENCE.md)

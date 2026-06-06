# Functional.Microsoft.OpenAPI.Extensions

F# functional-first extensions for [Microsoft.OpenApi](https://github.com/microsoft/OpenAPI.NET) v3.x. Null-safe adapters, active patterns, schema graph traversal, route map extraction, links graph traversal, configurable linting, spec merging, and subsetting.

> **Pre-release:** packages are currently published at `0.9.0`. APIs may change before a stable `1.0` release.

## Features

- **Null-safe adapters** — total functions over `IOpenApi*` interfaces; no null-reference surprises on paths, operations, schemas, references, or extensions
- **Active patterns** — declarative schema decomposition (`SchemaRef`, `ArraySchema`, `ObjectSchema`, `ComposedSchema`) for clean `match` expressions
- **Schema graph traversal** — build renderer-agnostic `SchemaGraph` IR with cycle detection across `allOf`/`oneOf`/`anyOf`, properties, and arrays
- **Route map extraction** — collect per-operation routes with parameter, request, and response schema references
- **Links graph traversal** — collect operation-to-operation links from response `links` objects into renderer-agnostic `LinksGraph` IR
- **Configurable linting** — 12 built-in rules for documentation, structure, and example validation (separate NuGet package)
- **Spec merging** — combine multiple OpenAPI documents into one (first-wins conflict resolution)
- **Spec subsetting (scissors)** — filter operations by tag, path, or operation id; optionally copy transitive component schemas
- **Graphviz visualization** — export schema graphs, route maps, and links graphs to DOT or SVG
- **CLI tool** — `openapi-fx` for visualization, linting, IR collection, merge, and scissors from the command line
- **Railway-oriented programming** — `Result`-based loaders and transforms composed via `ResultEx` helpers

## Packages

| Package | Description |
|---------|-------------|
| [Functional.Microsoft.OpenAPI.Extensions](https://www.nuget.org/packages/Functional.Microsoft.OpenAPI.Extensions) | Core library — adapters, traversal, merge, scissors |
| [Functional.Microsoft.OpenAPI.Extensions.Linting](https://www.nuget.org/packages/Functional.Microsoft.OpenAPI.Extensions.Linting) | Configurable lint rules and example validation |
| [Functional.Microsoft.OpenAPI.Extensions.Visualizing](https://www.nuget.org/packages/Functional.Microsoft.OpenAPI.Extensions.Visualizing) | Graphviz DOT/SVG export for schema graphs, route maps, and links graphs |
| [Functional.Microsoft.OpenAPI.Extensions.Tool](https://www.nuget.org/packages/Functional.Microsoft.OpenAPI.Extensions.Tool) | Global CLI tool (`openapi-fx`) |

## Quick Start

### Installation

```bash
dotnet add package Functional.Microsoft.OpenAPI.Extensions --version 0.9.0
```

Optional linting package:

```bash
dotnet add package Functional.Microsoft.OpenAPI.Extensions.Linting --version 0.9.0
```

Optional visualization package:

```bash
dotnet add package Functional.Microsoft.OpenAPI.Extensions.Visualizing --version 0.9.0
```

### Basic Usage

```fsharp
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiTraversal
open OpenApiOperationsTraversal
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Linter

let specPath = "Samples/petstore.yaml"

match readSpecification specPath with
| Error err -> printfn "Failed to load: %A" err
| Ok doc ->
    let graph = collectDocumentSchemas doc
    let routes = collectRouteMap doc
    let lintResult = lintWithDefaults doc
    printfn "Schema nodes: %d, edges: %d" graph.Nodes.Count graph.Edges.Count
    printfn "Routes: %d" routes.Routes.Count
    printfn "Lint violations: %d" lintResult.Violations.Length
```

See [docs/LINTING.md](docs/LINTING.md) for lint rule configuration and CLI usage.

## CLI Tool

### Installation

```bash
dotnet tool install --global Functional.Microsoft.OpenAPI.Extensions.Tool --version 0.9.0
```

After installation, the command is `openapi-fx`. You can also run from source without installing:

```bash
dotnet run --project Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool/ -- <flags>
```

### Commands

| Command | Description |
|---------|-------------|
| `--schema-svg` | Render the full schema graph (or a single component with `--component`) to SVG; add `--dot` for DOT output |
| `--route-svg` | Render a hub-and-spoke route map to SVG |
| `--links-svg` | Render operation links graph to SVG |
| `--schema-collect` | Export schema graph IR as JSON |
| `--route-collect` | Export route map IR as JSON |
| `--links-collect` | Export links graph IR as JSON |
| `--lint` | Lint a specification; use `--format json` for machine-readable output |
| `--merge` | Merge multiple specs into one document (repeat `--input` for each file) |
| `--scissors` | Cut a subset by `--include-tag`, `--include-path`, and/or `--include-operation` |
| `--version` | Print tool version and exit |

Route diagram options: `--center`, `--include-operations`, `--include-schemas`.

### Examples

Render a schema graph to SVG:

```bash
openapi-fx --schema-svg --input Samples/petstore.yaml --out out/schema.svg
```

Lint a specification (human-readable or JSON):

```bash
openapi-fx --lint --input Samples/petstore.yaml
openapi-fx --lint --input Samples/petstore.yaml --format json
```

Render a route map with operations and referenced schemas:

```bash
openapi-fx --route-svg --input Samples/petstore.yaml --out out/routes.svg \
  --center "Petstore API" --include-operations --include-schemas
```

Cut a spec to pets-related operations and save the result:

```bash
openapi-fx --scissors --input Samples/petstore.yaml --out out/cut.yaml \
  --include-tag pets --include-path /pets --include-operation showPetById
```

## Architecture

The library is organized in layers with a strict separation between data collection (IR) and rendering:

```
AdapterCore
    ↓
ActivePatterns → SchemaAdapters / ReferenceAdapters / OperationAdapters /
                 DocumentAdapters / ExtensionAdapters
    ↓
OpenApiAdapters (aggregate traversal helpers)
    ↓
OpenApiTraversal           → SchemaGraph IR (nodes, edges, cycle-safe)
OpenApiOperationsTraversal → RouteMap IR (paths, methods, schema refs)
OpenApiLinksTraversal      → LinksGraph IR (operation links)
    ↓
OpenApiMerge / OpenApiScissors / OpenApiWriterTools
Linting (separate package) → LintResult
    ↓
Visualizing (GraphvizExport) → DOT / SVG rendering only
```

- **Adapters** convert nullable OpenAPI.NET collections and properties into F# `option`, `Map`, `list`, and `Set` at the API boundary.
- **Traversal modules** produce renderer-agnostic intermediate representation; they never contain Graphviz or display logic.
- **Linting** is a separate package that inspects documents and returns `LintResult` violations — no rendering logic.
- **Visualizing** consumes IR exclusively — schema node labels include type, nullability, format, and composition; route diagrams default to hub + routes with optional operation and schema layers.

All public workflows return `Result<_,_>` (railway-oriented programming). Compose with `Result.bind` and helpers in `ResultEx`.

## Requirements

- .NET 10.0
- [Microsoft.OpenApi](https://www.nuget.org/packages/Microsoft.OpenApi) 3.6+ (with `Microsoft.OpenApi.YamlReader` for YAML input)
- For SVG rendering: [Graphviz](https://graphviz.org/) (`dot` executable in `PATH`)

## Building from Source

```bash
dotnet build
dotnet test
```

## License

MIT

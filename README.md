## Microsoft.OpenAPI.FunctionalExtensions

Functional F# utilities and tools on top of Microsoft.OpenApi for traversing, analyzing, and visualizing OpenAPI specifications. This README is in English per project preference [[memory:5983738]].

### Overview

- Core library: traversal helpers for schemas (incl. allOf/oneOf/anyOf) and operations; ROP-style flows using FSharp.Core `Result` + helpers in `ResultEx`.
- Visualization: Graphviz SVG export using `Rubjerg.Graphviz`.
- CLI: quick routes/schemas visualizations for large specs.

Inspiration and target UX come from the reference ideas in `IdeasSource/GraphVisualization/`:
- TypeScript-based route visualization: `IdeasSource/GraphVisualization/ApiMapperJs/`
- Graphviz-based schema visualization: `IdeasSource/GraphVisualization/openapischematograph-master/`

We will mirror those UX patterns: base view shows route hub + routes only; optional layers add operations and referenced schemas.

### Quick start

Prereqs: .NET 9 SDK. This repo includes a small sample `Samples/petstore.yaml`.

Build and test:

```bash
dotnet build Microsoft.OpenAPI.FunctionalExtensions.sln
dotnet test  Microsoft.OpenAPI.FunctionalExtensions.sln
```

### CLI usage (Visualizing.Tool)

Render schema graph to SVG (works from any directory):

```bash
dotnet run --project /home/vladimir/GitRoot/F/Microsoft.OpenAPI.FunctionalExtensions/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool -- \
  --schema-svg --input /home/vladimir/GitRoot/F/Microsoft.OpenAPI.FunctionalExtensions/Samples/petstore.yaml --out /home/vladimir/GitRoot/F/Microsoft.OpenAPI.FunctionalExtensions/out/schema.svg
```

Render route map to SVG (base: hub + routes only; works from any directory):

```bash
dotnet run --project /home/vladimir/GitRoot/F/Microsoft.OpenAPI.FunctionalExtensions/Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool -- \
  --route-svg --input /home/vladimir/GitRoot/F/Microsoft.OpenAPI.FunctionalExtensions/Samples/petstore.yaml --out /home/vladimir/GitRoot/F/Microsoft.OpenAPI.FunctionalExtensions/out/routes.svg
```

Advanced route options:

```bash
# Custom hub label
--center "Petstore API"

# Add operation nodes under each route
--include-operations

# Add referenced schema nodes (parameters/request/responses)
--include-schemas

# Example (all options):
dotnet run --project Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool -- \
  --route-svg --input Samples/petstore.yaml --out out/routes_full.svg \
  --center "Petstore API" --include-operations --include-schemas
```

Notes:
- Schema labels omit noisy prefixes (e.g., `#/components/schemas/Person` → `Person`).
- Property nodes include: type, array item type (`array of X`), nullability (`?`), format (when present), readOnly marker, and enum values.
- Edge labels: `propertyName`, `items`, `map value`, `allOf|oneOf|anyOf`.
- Route IR tracks `ReturnsArray`, `ReturnsArrayViaData`, `HasOperations` for future highlighting.

### Roadmap (visualization)

- Align route layout and annotations with the TypeScript prototype (hub, per-path nodes; optional operations; optional schema links).
- Expand schema reference discovery across all operation parts (params, request bodies, responses, headers, callbacks, links, etc.).
- Snapshot tests for SVG outputs via Verify.

### License

MIT



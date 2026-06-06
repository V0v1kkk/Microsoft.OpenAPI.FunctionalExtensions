# Agent Guidelines for this Repository

## Project Overview

F# functional-first extensions for [Microsoft.OpenApi](https://github.com/microsoft/OpenAPI.NET) v3.6+. The solution provides null-safe adapters, schema/route/links traversal IR, configurable linting with example validation, Graphviz visualization, and a CLI tool (`openapi-fx`).

**Target framework:** .NET 9.0  
**Pre-release:** packages ship as `0.2.0-alpha` (see `Directory.Build.props` for version prefix/suffix).

## Architecture

Solution layout (dependency order):

| Project | NuGet / role |
|---------|--------------|
| `Microsoft.OpenAPI.FunctionalExtensions/` | **Core** — adapters, active patterns, traversal IR, merge, scissors, readers/writers |
| `Microsoft.OpenAPI.FunctionalExtensions.Linting/` | **Linting** — configurable lint rules, example validation |
| `Microsoft.OpenAPI.FunctionalExtensions.Visualizing/` | **Visualizing** — Graphviz DOT/SVG export (consumes IR only) |
| `Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool/` | **CLI** — `openapi-fx` command-line interface |
| `Tests/Microsoft.OpenAPI.FunctionalExtensions.Tests/` | Core + linting unit, property, and snapshot tests |
| `Tests/Microsoft.OpenAPI.FunctionalExtensions.Visualizing/` | CLI integration tests (requires Graphviz `dot` in PATH) |

Layering (data collection is separate from rendering):

```
AdapterCore → ActivePatterns → *Adapters → Traversal (SchemaGraph / RouteMap IR)
    → Merge / Scissors / WriterTools → Visualizing (Graphviz) → DOT / SVG
```

- Traversal modules work against `IOpenApi*` interfaces and produce renderer-agnostic IR.
- Visualizing never touches raw OpenAPI models — only IR from traversal.
- All public workflows return `Result<_,_>`; compose with `ResultEx` helpers.

## Development Workflow

### Test-Driven Development (TDD)

All features must be developed using TDD:

1. Write failing tests first (unit tests, snapshot tests, property-based tests).
2. Implement the minimum code to make tests pass.
3. Refactor while keeping tests green.

### Testing Requirements

- **Unit tests:** NUnit 4, one test module per feature area under `Tests/Microsoft.OpenAPI.FunctionalExtensions.Tests/`.
- **Snapshot tests:** Verify.NUnit for IR/DOT/SVG outputs (`SnapshotTests.fs` and related).
- **Property-based tests:** FsCheck for adapter robustness, null-safety, and type invariants (`PropertyTests.fs`).
- **Integration tests:** CLI subprocess tests with timeout guards in `Tests/Microsoft.OpenAPI.FunctionalExtensions.Visualizing/`.

Run the full local test suite:

```bash
dotnet test
```

Core-only (matches CI — no Graphviz required):

```bash
dotnet test Tests/Microsoft.OpenAPI.FunctionalExtensions.Tests/
```

The full solution test run should complete in under ~15 seconds on a typical dev machine. Visualizing tests are skipped in CI because they need the native Graphviz binary.

### Package Version Management

**IMPORTANT:** All NuGet package versions are managed centrally via Central Package Management.

- Package versions are defined in `Directory.Packages.props` (including `FSharp.Core`, pinned above transitive test-package versions).
- Shared build metadata (version prefix/suffix, authors, copyright, license) and the solution-wide `FSharp.Core` reference live in `Directory.Build.props`.
- **Do NOT** add `Version="..."` on `<PackageReference>` in individual `.fsproj` files.
- To update a package: edit `Directory.Packages.props` only.
- To add a new package: add `<PackageVersion Include="..." Version="..." />` in `Directory.Packages.props`, then add `<PackageReference Include="..." />` (no Version) in the relevant `.fsproj`.

### Build Properties

- **Central:** `Directory.Build.props` — `VersionPrefix`, `VersionSuffix`, `Authors`, `Copyright`, `PackageLicenseExpression`, `PackageProjectUrl`.
- **Per-project:** `PackageId`, `Description`, `PackageTags`, `ToolCommandName`, etc. stay in each `.fsproj`.

```bash
dotnet build
dotnet test
```

## Code Style

Read `.cursor/rules/project-rule.mdc` for mandatory engineering rules. Highlights:

- F# functional-first: immutability, `Result`/`Option`, pattern matching, active patterns.
- Railway-Oriented Programming: no exceptions for control flow; use `ResultEx.tryCatch` at boundaries.
- OpenAPI.NET v3.6+ APIs: prefer `IOpenApi*` interfaces, `HttpMethod`, `JsonSchemaType` flags, `JsonNode` extensions.
- Use project adapter modules (`OpenApiAdapterCore`, `OpenApiActivePatterns`, `*Adapters`) — never direct null checks on OpenAPI models.
- See `.agents/skills/fsharp-style/SKILL.md` for Vladimir's preferred F# code shapes.

## Documentation Updates

When modifying code, update documentation accordingly:

| Change | Update |
|--------|--------|
| New lint rules | `docs/LINTING.md` — rule ID, description, severity |
| New CLI commands/flags | `README.md` CLI section |
| New adapters/modules | `docs/skills/openapi-fsharp-extensions/REFERENCE.md` |
| API usage examples | `docs/skills/openapi-fsharp-extensions/EXAMPLES.md` |
| Architecture changes | This file and `README.md` |

## Commit Conventions

- Small, logical commits with descriptive messages.
- Message format: imperative mood; explain **what** and **why**.
- Run `dotnet build && dotnet test` before committing.
- Do not commit generated artifacts (`bin/`, `obj/`, `out/`, Verify snapshot `.received.*` files).

## CI/CD

Defined in `.github/workflows/`:

- **CI** (`ci.yml`): runs on push to `master`, `v*` tags, and pull requests. Builds the solution and runs `Tests/Microsoft.OpenAPI.FunctionalExtensions.Tests/` only.
- **Publish** (`publish.yml`): triggers on `v*` tags (and manual dispatch). Builds Release, runs core tests, packs NuGet packages, pushes to NuGet.org.

Local development requires .NET 9 SDK. SVG rendering additionally requires [Graphviz](https://graphviz.org/) (`dot` in `PATH`).

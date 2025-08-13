## Microsoft.OpenAPI.FunctionalExtensions — Project Plan

### Goals and Scope

- **Purpose**: Functional extensions and utilities around `Microsoft.OpenApi` to make it easier to traverse, analyze, visualize, diff, and merge OpenAPI specifications at scale.
- **Primary focuses (now)**:
  - Core traversal and helper abstractions for schemas and operations
  - Data collection for visualization (schema graphs and route maps), separated from rendering
  - Graphviz DOT exporter for quick visualization
  - Robust tests and examples
- **Secondary/next focuses (later)**:
  - Linting (rule engine, DSL, configurable severities, baselines)
  - Merging multiple specifications with conflict resolution
  - Optional XMind export when a workable path is identified
  - Code-generation experiments (from `IdeasSource`) in a separate track

### Guiding Principles

- **Functional-first**: Pure functions where practical, immutable data, explicit inputs/outputs, composable pipelines.
- **Safety**: Prefer total functions and typed results over exceptions. Use `Result`-based flows.
- **Railway Oriented Programming (ROP)**: Follow Scott Wlaschin’s approach (F# for Fun and Profit, Domain Modeling Made Functional). Use `Result`/`Async<Result<_,_>>` workflows, small composable functions, and clear error propagation. Complement F#’s built-in `Result` with Wlaschin-style helpers where missing.
- **Separation of concerns**: Clearly separate data collection (IR) from rendering/export.
- **Interoperability**: Embrace `Microsoft.OpenApi` models; add thin, well-typed F# layers only as needed.
- **Testability**: Unit, integration, and snapshot tests for stability and regression protection.

### Repository Structure (planned)

- `Microsoft.OpenAPI.FunctionalExtensions/` — core library (extensions, traversal, diff/merge utilities, IR types)
- `Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool/` — CLI for data collection and rendering (Graphviz DOT initially)
- `Microsoft.OpenAPI.FunctionalExtensions.Linting/` — linting engine (skeleton now, future work later)
- `Microsoft.OpenAPI.FunctionalExtensions.Converter.Tool/` — archived/deprioritized for now
- `Tests/` — unit/integration/snapshot tests per project
- `Samples/` — example OpenAPI documents (small/medium/large)
- `IdeasSource/` — reference prototypes (C#, JS, T4); used as inspiration only
- `PLAN.md` — this document

### Milestones and Deliverables

#### Milestone 0 — Housekeeping and Baseline

- [ ] Ensure solution references are consistent; projects restore and build
- [ ] Establish `Samples/` with a few canonical OpenAPI specs (minimal, medium, complex)
- [ ] Decide and add dependencies:
  - [ ] CLI args parser: `Argu` (preferred) or `Spectre.Console.Cli`
  - [ ] Testing: `NUnit` + `FsCheck` (via `FsCheck.NUnit`) + snapshot testing (`Verify.NUnit`)
  - [ ] Graphviz export: plain DOT text emission (no external lib needed)
- [ ] Enable nullable annotations where applicable and consistent F# analyzers
- [ ] Wire basic CI (build + test) via GitHub Actions
- [ ] Curate additional public sample OpenAPI specs and/or scripts to fetch them (small/medium/large, multiple domains)

Acceptance:
- Build succeeds locally and in CI; tests project scaffolds exist

#### Milestone 1 — Core Foundations (Library)

- [ ] Result helpers and error types finalized (`Results.fs`)
- [ ] Adopt ROP throughout public APIs: prefer `Result`/`AsyncResult` over exceptions
- [ ] Extend built-in `Result` with Wlaschin-style combinators in modules (e.g., `ResultEx`, `AsyncResult`): `map`, `bind`, `mapError`, `bindError`, `tee`, `apply`, `sequence`, `traverse`, `ofOption`, `toOption`
- [ ] OpenAPI document loading utilities with clear error reporting
- [ ] JSON Pointer helpers for stable node identity
- [ ] Reference resolution utilities (wrapper helpers over `Microsoft.OpenApi.Models`) without mutating original documents

Acceptance:
- Unit tests cover loading success/failure, pointer helpers, and reference resolution basics

#### Milestone 2 — Traversal Utilities (Schemas and Operations)

- [ ] Schema traversal that safely handles:
  - [ ] `$ref` resolution, cycles, and visited-cache
  - [ ] Objects (properties, required), arrays (items), maps (`additionalProperties`), compositions (support `allOf`/`oneOf`/`anyOf` consistently across traversal, IR, exporters, and merge)
  - [ ] Discriminators and polymorphism hints
- [ ] Operations traversal over `Paths` → `PathItem` → `Operation`s, including parameters, request bodies, and responses
- [ ] Extensible visitors (pure functions) and iterators with filtering predicates

Acceptance:
- Unit tests with synthetic specs covering edge cases (cycles, deep refs, mixed compositions)

#### Milestone 3 — Intermediate Representations (IR)

- [ ] Schema Graph IR:
  - [ ] Nodes: stable id (JSON pointer), label (title/name), kind, metadata (nullable, description, format)
  - [ ] Edges: typed relations (property, arrayItem, mapValue, `allOf`/`oneOf`/`anyOf`)
  - [ ] Options for collapsing trivial nodes (e.g., primitives) and filtering
- [ ] Route Map IR:
  - [ ] Routes: path + method, tags, operationId, security
  - [ ] Links to schemas used by parameters/request/response
- [ ] Common filtering/projection options (by tag, by path prefix, by component prefix)

Acceptance:
- Golden JSON snapshots for IR generated from `Samples/` specs

#### Milestone 4 — DOT Exporters (Rendering Layer 1)

- [ ] DOT exporter for Schema Graph IR (Graphviz)
- [ ] DOT exporter for Route Map IR
- [ ] Theming hooks: simple shapes/colors by node/edge type
- [ ] Output as `.dot` files or stdout; user can render via `dot` binary

Acceptance:
- Snapshot tests for DOT output from `Samples/`

#### Milestone 5 — CLI (Visualizing.Tool)

- [ ] CLI commands (using `Argu`):
  - [ ] `schema-graph collect` → JSON IR
  - [ ] `schema-graph dot` → DOT from IR
  - [ ] `route-map collect` → JSON IR
  - [ ] `route-map dot` → DOT from IR
- [ ] Global options: input file(s), resolve external refs option, filters (tags, paths), output target
- [ ] Exit codes and error messages are consistent and machine-friendly

Acceptance:
- End-to-end tests invoking CLI against `Samples/` producing expected IR/DOT

#### Milestone 6 — Merge Utilities (Library)

- [ ] Merge algorithms for multiple OpenAPI documents:
  - [ ] Components merge with conflict strategies (rename, prefer-first, fail)
  - [ ] Paths/operations merge with idempotency checks
  - [ ] Shared servers/security/commons reconciliation
- [ ] Deterministic merge order and stable outputs
- [ ] Optional: CLI wrapper (`merge`) to produce a combined spec

Acceptance:
- Integration tests: merging known inputs equals golden combined spec

#### Milestone 7 — Linting (Skeleton now, Roadmap later)

- [ ] Rule engine scaffold:
  - [ ] Rule type, metadata (id, title, description, default severity)
  - [ ] Rule registry with composition/filtering
  - [ ] Execution with results (location pointer, message, severity)
- [ ] A few initial rules (example set):
  - [ ] Missing `description` on operations or schemas
  - [ ] Unused components/schemas
  - [ ] Duplicate parameter definitions on operations
  - [ ] Inconsistent naming (simple regex-based heuristic)
- [ ] Config (YAML/JSON): enable/disable rules, severity overrides, ignore patterns, baseline support

Acceptance:
- Unit tests for rules; CLI (later) can print SARIF/JSON

#### Milestone 8 — Documentation and Samples

- [ ] Update `README` and examples on how to use the library and CLI
- [ ] Add sample visual outputs (PNG/SVG rendered from DOT) to docs
- [ ] API docs generated (e.g., DocFX) for the core library

Acceptance:
- Docs understandable end-to-end with reproducible commands

#### Milestone 9 — Packaging, CI/CD, and Docker

- [ ] Publish core library to NuGet (semver, release notes, symbol/package source)
- [ ] Optionally publish CLI as a .NET tool package
- [ ] Docker images for CLI workflows (Graphviz included or documented)
- [ ] CI releases on tagged commits

Acceptance:
- `dotnet tool install` (if chosen) works; Docker image runs the CLI

### CLI Design (Visualizing.Tool)

Examples:

```bash
dotnet run --project Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool -- \
  schema-graph collect --input ./Samples/petstore.yaml --out ./out/schema.json

dotnet run --project Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool -- \
  schema-graph dot --input ./Samples/petstore.yaml --out ./out/schema.dot --filter-tags Pet

dotnet run --project Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool -- \
  route-map dot --input ./Samples/petstore.yaml --out ./out/routes.dot

dot -Tsvg ./out/schema.dot -o ./out/schema.svg
```

Command structure:

- `schema-graph collect` — produce Schema Graph IR (JSON)
- `schema-graph dot` — emit DOT directly
- `route-map collect` — produce Route Map IR (JSON)
- `route-map dot` — emit DOT directly

Common options:

- `--input <path>` or multiple `--input` for merging flows
- `--out <path>` or stdout when omitted
- Filters: `--filter-tags`, `--filter-path-prefix`, `--filter-components-prefix`
- `--resolve-external-refs` to resolve `$ref` across files

Exit codes:

- `0` success; `2` invalid input; `3` merge conflict; `4` runtime error

### Testing Strategy

- **Unit tests**: NUnit-based tests for pure traversal helpers, pointer utilities, IR construction
- **Integration tests**: load `Samples/` specs and assert IR shape/size; diff/merge behaviors
- **Snapshot tests**: IR JSON and DOT outputs (golden files) via `Verify.NUnit`
- **Property-based tests**: schema generation (FsCheck with `FsCheck.NUnit`) to stress traversal and cycle handling
- **CLI tests**: invoke process with arguments; assert exit code and output content

### Linting Roadmap (Details)

- **Rule categories**:
  - Completeness: missing `description`, missing examples, empty tags
  - Hygiene: unused components/schemas/parameters, dead `operationId`s
  - Consistency: naming conventions, status code coverage (2xx/4xx/5xx)
  - Design: overly deep nesting, very large schemas, too many parameters
  - References: broken `$ref`, cyclic references (where illegal), ambiguous discriminators
- **Engine features**:
  - Config-driven severity and rule toggling
  - Baseline files to suppress preexisting findings
  - SARIF/JSON output for CI integrations

### Merge Strategy (Details)

- Normalize inputs (servers, info, version) and converge on a target structure
- Components:
  - Name conflict strategies: suffixing, prefixing by source id, or fail-fast
  - Deep equality checks to deduplicate identical components
- Paths/operations:
  - Merge when identical or non-overlapping; detect conflicting methods
  - Combine parameters/request/response with deterministic precedence
- Produce deterministic, stable output with sorted keys

### Engineering Conventions

- F# style: descriptive names, small pure functions, pipeline-friendly modules
- Avoid exceptions for control flow; return `Result<'ok,'err>` types
- Keep public APIs explicit and documented; internal helpers separated
- Do not reformat unrelated code during edits; preserve existing style

### Non-Goals (for now)

- v2-to-v3 conversion (keep `Converter.Tool` but do not invest further)
- Full XMind automation until a robust .NET path exists
- Full-blown UI; focus on CLI + DOT for now

### Next Steps (Initial Sprint Checklist)

- [ ] Milestone 0: ensure build, add `Samples/`, pin dependencies, set up CI
- [ ] Milestone 1–2: implement traversal utilities and tests
- [ ] Milestone 3: define IR models + snapshots
- [ ] Milestone 4–5: DOT exporters + CLI commands (schema/route)

### References

- `Microsoft.OpenApi` (OpenAPI.NET) library for model types
- Graphviz (`dot`) for rendering DOT files
- `Argu` for F#-friendly CLI parsing
- `NUnit` for test harness; `FsCheck` + `FsCheck.NUnit` for property-based testing; `Verify.NUnit` for snapshots

### Research Backlog

- Evaluate F#-friendly visualization options in addition to plain DOT emission:
  - Graph layout/render: MSAGL (Microsoft Automatic Graph Layout), Graphviz4Net, DotNetGraph
  - Text-based formats: Mermaid generation as an alternative to DOT
- Decide based on API ergonomics from F#, capabilities, and output quality. Keep initial implementation on simple DOT emission for reliability.



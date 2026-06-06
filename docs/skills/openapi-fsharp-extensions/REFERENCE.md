# OpenAPI F# Extensions — API Reference

Namespace: `Microsoft.OpenAPI.FunctionalExtensions.*` unless noted.  
Packages: `Functional.Microsoft.OpenAPI.Extensions` (core), `Functional.Microsoft.OpenAPI.Extensions.Linting` (linting), `Functional.Microsoft.OpenAPI.Extensions.Visualizing` (Graphviz).

---

## Module dependency graph

| Module | Depends on |
|--------|------------|
| `AdapterCore` | — |
| `ResultEx`, `SeqExtensions`, `StringExtensions` | — |
| `OpenApiTools` | `StringExtensions`, `SeqExtensions` |
| `Readers.Types` | `Microsoft.OpenApi` |
| `OpenApiReaderTools` | `ResultEx`, `Readers.Types`, `Microsoft.OpenApi.Reader` |
| `OpenApiSchemaAnalysis` | `Microsoft.OpenApi` |
| `ActivePatterns` | `OpenApiSchemaAnalysis` |
| `SchemaAdapters` | `AdapterCore` |
| `ReferenceAdapters` | — |
| `OperationAdapters` | `AdapterCore` |
| `DocumentAdapters` | `AdapterCore`, `OperationAdapters` |
| `ExtensionAdapters` | `AdapterCore`, `StringExtensions` |
| `OpenApiAdapters` | `OpenApiSchemaAnalysis` |
| `OpenApiTraversal` | `OpenApiAdapters`, `OpenApiSchemaAnalysis` |
| `OpenApiOperationsTraversal` | `ActivePatterns`, `ReferenceAdapters`, `OperationAdapters`, `DocumentAdapters`, `SchemaAdapters` |
| `OpenApiLinksTraversal` | `DocumentAdapters`, `OperationAdapters`, `AdapterCore` |
| `OpenApiMerge` | `Microsoft.OpenApi.Reader` |
| `Linting.*` | Core adapters, `Microsoft.OpenApi` |
| `OpenApiScissors` | `OpenApiSchemaAnalysis` |
| `OpenApiWriterTools` | `Microsoft.OpenApi` |
| `GraphvizExport` | `OpenApiTraversal`, `OpenApiOperationsTraversal`, `Rubjerg.Graphviz` |

---

## AdapterCore

`Microsoft.OpenAPI.FunctionalExtensions.AdapterCore` — `[<RequireQualifiedAccess>]`

| Function | Signature | Returns |
|----------|-----------|---------|
| `ofObj` | `'T -> 'T option` | `None` if null |
| `ofNullable` | `Nullable<'T> -> 'T option` | `None` if no value |
| `readMap` | `IDictionary<'k,'v> -> Map<'k,'v>` | Empty map if null; skips null values |
| `readSeq` | `#seq<'T> -> 'T list` | Empty list if null; skips null elements |
| `readSet` | `ISet<'T> -> Set<'T>` | Empty set if null |

---

## ActivePatterns

`Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns`

| Pattern | Input | Output | Matches |
|---------|-------|--------|---------|
| `SchemaRef` | `IOpenApiSchema` | `string option` | Component reference id |
| `ArraySchema` | `IOpenApiSchema` | `IOpenApiSchema option` | Array type or has `Items` |
| `ObjectSchema` | `IOpenApiSchema` | `IDictionary<string, IOpenApiSchema> option` | Non-empty properties |
| `ComposedSchema` | `IOpenApiSchema` | `(CompositionKind * IOpenApiSchema list) option` | First non-empty of allOf/oneOf/anyOf |
| `NotNull` | `'T` (reference) | `'T option` | Non-null reference |
| `NullableType` / `NonNullableType` | `Nullable<JsonSchemaType>` | split | With/without `JsonSchemaType.Null` flag |

`CompositionKind`: `AllOf` \| `OneOf` \| `AnyOf` (separate from `OpenApiTraversal.CompositionKind`).

---

## SchemaAdapters

`Microsoft.OpenAPI.FunctionalExtensions.SchemaAdapters` — `[<RequireQualifiedAccess>]`

| Function | Signature | Description |
|----------|-----------|-------------|
| `schemaTitle` | `IOpenApiSchema -> string option` | Title |
| `schemaDescription` | `IOpenApiSchema -> string option` | Description |
| `schemaType` | `IOpenApiSchema -> JsonSchemaType option` | Type flags |
| `schemaFormat` | `IOpenApiSchema -> string option` | Format string |
| `schemaIsNullable` | `IOpenApiSchema -> bool` | `JsonSchemaType.Null` flag present |
| `schemaProperties` | `IOpenApiSchema -> Map<string, IOpenApiSchema>` | Properties map |
| `schemaRequired` | `IOpenApiSchema -> Set<string>` | Required property names |
| `schemaItems` | `IOpenApiSchema -> IOpenApiSchema option` | Array items schema |
| `schemaAllOf` | `IOpenApiSchema -> IOpenApiSchema list` | allOf list |
| `schemaOneOf` | `IOpenApiSchema -> IOpenApiSchema list` | oneOf list |
| `schemaAnyOf` | `IOpenApiSchema -> IOpenApiSchema list` | anyOf list |
| `schemaNot` | `IOpenApiSchema -> IOpenApiSchema option` | not schema |
| `schemaAdditionalProperties` | `IOpenApiSchema -> IOpenApiSchema option` | additionalProperties |
| `schemaEnum` | `IOpenApiSchema -> JsonNode list` | Enum values |
| `schemaReadOnly` | `IOpenApiSchema -> bool` | readOnly flag |
| `schemaWriteOnly` | `IOpenApiSchema -> bool` | writeOnly flag |
| `schemaDeprecated` | `IOpenApiSchema -> bool` | deprecated flag |

---

## ReferenceAdapters

`Microsoft.OpenAPI.FunctionalExtensions.ReferenceAdapters` — `[<RequireQualifiedAccess>]`

| Function | Signature | Description |
|----------|-----------|-------------|
| `trySchemaReferenceId` | `IOpenApiSchema -> string option` | Component name from `$ref` |
| `referencePointer` | `string -> string` | `#/components/schemas/{id}` |
| `tryResolveSchemaReference` | `OpenApiDocument -> IOpenApiSchema -> IOpenApiSchema option` | Resolve against `doc.Components.Schemas` |
| `isUnresolvedReference` | `IOpenApiSchema -> bool` | True for unresolved `OpenApiSchemaReference` |

---

## OperationAdapters

`Microsoft.OpenAPI.FunctionalExtensions.OperationAdapters` — `[<RequireQualifiedAccess>]`

| Function | Signature | Description |
|----------|-----------|-------------|
| `pathItemOperations` | `IOpenApiPathItem -> (HttpMethod * OpenApiOperation) list` | HTTP methods + operations |
| `operationTags` | `OpenApiOperation -> string list` | Tag names |
| `operationParameters` | `OpenApiOperation -> IOpenApiParameter list` | Parameters |
| `operationRequestBody` | `OpenApiOperation -> IOpenApiRequestBody option` | Request body |
| `operationResponses` | `OpenApiOperation -> Map<string, IOpenApiResponse>` | Responses by status code |
| `mediaTypeSchema` | `IOpenApiMediaType -> IOpenApiSchema option` | Schema from media type |
| `schemasFromContent` | `IDictionary<string, IOpenApiMediaType> -> IOpenApiSchema list` | All content schemas |
| `schemasFromOperation` | `OpenApiOperation -> IOpenApiSchema list` | Params + request + response schemas |

---

## DocumentAdapters

`Microsoft.OpenAPI.FunctionalExtensions.DocumentAdapters` — `[<RequireQualifiedAccess>]`

| Function | Signature | Description |
|----------|-----------|-------------|
| `documentComponents` | `OpenApiDocument -> OpenApiComponents option` | Components section |
| `documentSchemas` | `OpenApiDocument -> Map<string, IOpenApiSchema>` | `#/components/schemas` |
| `documentResponses` | `OpenApiDocument -> Map<string, IOpenApiResponse>` | Component responses |
| `documentParameters` | `OpenApiDocument -> Map<string, IOpenApiParameter>` | Component parameters |
| `documentRequestBodies` | `OpenApiDocument -> Map<string, IOpenApiRequestBody>` | Component request bodies |
| `documentHeaders` | `OpenApiDocument -> Map<string, IOpenApiHeader>` | Component headers |
| `documentServers` | `OpenApiDocument -> OpenApiServer list` | Servers |
| `documentTags` | `OpenApiDocument -> string list` | Document-level tag names |
| `documentPaths` | `OpenApiDocument -> (string * IOpenApiPathItem) list` | All paths |
| `foldAllOperations` | `OpenApiDocument -> ('s -> string * HttpMethod * OpenApiOperation -> 's) -> 's -> 's` | Fold over all operations |
| `allOperations` | `OpenApiDocument -> (string * HttpMethod * OpenApiOperation) list` | All operations flat list |
| `tryComponentSchema` | `OpenApiDocument -> string -> IOpenApiSchema option` | Lookup component schema by name |

---

## ExtensionAdapters

`Microsoft.OpenAPI.FunctionalExtensions.ExtensionAdapters` — `[<RequireQualifiedAccess>]`

| Function | Signature | Description |
|----------|-----------|-------------|
| `extensions` | `IDictionary<string, IOpenApiExtension> -> Map<string, IOpenApiExtension>` | Extensions map |
| `tryExtensionJsonNode` | `IDictionary<_,_> -> string -> JsonNode option` | Extension as JsonNode |
| `tryExtensionString` | `IDictionary<_,_> -> string -> string option` | Extension as string |
| `extensionIsTruthy` | `IDictionary<_,_> -> string -> bool` | `"true"`, `"1"`, or `"yes"` |

### OpenApiTools (no RequireQualifiedAccess)

| Function | Signature | Description |
|----------|-----------|-------------|
| `tryGetValue` | `obj -> string option` | Recursive JsonNode → string |
| `getExtensionValue` | `IDictionary<string, IOpenApiExtension> -> string -> string option` | Named extension value |
| `operationHasExtensionWithTrueValue` | `OpenApiOperation -> string -> bool` | Extension equals `"true"` |

---

## OpenApiAdapters (aggregate)

`Microsoft.OpenAPI.FunctionalExtensions.OpenApiAdapters`

| Function | Signature | Description |
|----------|-----------|-------------|
| `componentsSchemas` | `OpenApiDocument -> IDictionary<string, IOpenApiSchema> option` | Raw components schemas dict |
| `tryGetComponentSchema` | `OpenApiDocument -> string -> IOpenApiSchema option` | Component lookup |
| `trySchemaRefName` | `IOpenApiSchema -> string option` | Reference id via schema analysis |
| `schemaChildren` | `IOpenApiSchema -> seq<IOpenApiSchema>` | Properties, items, compositions |
| `schemasFromContent` | `IDictionary<string, IOpenApiMediaType> -> seq<IOpenApiSchema>` | Lazy content schemas |
| `schemasFromOperation` | `OpenApiOperation -> seq<IOpenApiSchema>` | Lazy operation schemas |
| `foldPaths` | `OpenApiDocument -> ('s -> string * IOpenApiPathItem -> 's) -> 's -> 's` | Fold paths |
| `foldOperations` | `OpenApiDocument -> ('s -> string * HttpMethod * OpenApiOperation -> 's) -> 's -> 's` | Fold operations |

---

## OpenApiReaderTools

`Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools`

| Function | Signature | Description |
|----------|-----------|-------------|
| `readSpecification` | `string -> Result<OpenApiDocument, ReaderError>` | Load YAML/JSON file |

Internal pipeline: `validateFilePath` → `readFileText` → `convertToSpecification` (YAML reader registered).

### Readers.Types

| Type | Cases / Fields |
|------|----------------|
| `SpecificationFilePath` | `SpecificationFilePath of string` |
| `OpenApiError` | `OpenApiError of Message: string * Location: string` |
| `ReaderError` | `FileNotFound` \| `FileReadError` \| `OpenApiErrors` |
| `ReadSpecification` | Type alias for loader function |

---

## OpenApiTraversal

Module `OpenApiTraversal` (global module name).

### IR types

| Type | Fields |
|------|--------|
| `SchemaNodeRef` | `string` (JSON pointer or component ref) |
| `SchemaNode` | `Id`, `Title`, `Kind`, `Nullable`, `Description`, `Format`, `ReadOnly`, `EnumValues`, `WriteOnly` |
| `SchemaEdgeKind` | `Property of string` \| `ArrayItem` \| `MapValue` \| `Composition of CompositionKind` |
| `SchemaEdge` | `FromId`, `ToId`, `EdgeKind` |
| `SchemaGraph` | `Nodes: ResizeArray<SchemaNode>`, `Edges: ResizeArray<SchemaEdge>` |
| `CompositionKind` | `AllOf` \| `OneOf` \| `AnyOf` |

### Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `collectSchemaGraph` | `IOpenApiSchema -> SchemaGraph` | Graph from root (pointer `#`) |
| `collectSchemaGraphWithRoot` | `IOpenApiSchema -> string -> SchemaGraph` | Graph with custom root pointer |
| `collectDocumentSchemas` | `OpenApiDocument -> SchemaGraph` | Union of all component schema graphs |

Uses `HashSet` visited cache for cycles.

---

## OpenApiOperationsTraversal

Module `OpenApiOperationsTraversal` (global module name).

### IR types

| Type | Fields |
|------|--------|
| `Route` | `Path`, `Method`, `OperationId`, `Tags`, `ParameterSchemas`, `RequestSchemas`, `ResponseSchemas`, `ReturnsArray`, `ReturnsArrayViaData`, `HasOperations` |
| `RouteMap` | `Routes: ResizeArray<Route>` |

Schema ref fields are JSON pointers (`#/components/schemas/{name}`).

### Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `collectRouteMap` | `OpenApiDocument -> RouteMap` | All operations → routes with schema refs |

---

## OpenApiLinksTraversal

Module `OpenApiLinksTraversal` (global module name).

### IR types

| Type | Fields / Cases |
|------|----------------|
| `LinkSource` | `ResponseBody of jsonPointer` \| `ResponseHeader of headerName` \| `RequestBody of jsonPointer` |
| `LinkTarget` | `OperationParameter of parameterName` \| `RequestBodyField of jsonPointer` |
| `OperationLink` | `LinkName`, `SourceOperationId`, `TargetOperationId`, `Source`, `Target`, `Description` |
| `LinksGraph` | `Operations: string list`, `Links: OperationLink list` |

### Functions

| Function | Signature | Description |
|----------|-----------|-------------|
| `collectLinksGraph` | `OpenApiDocument -> LinksGraph` | Collect operation links from response `links` objects |

Parses runtime expressions (`$response.body#/…`, `$response.header.…`, `$request.body#/…`) into `LinkSource`/`LinkTarget` pairs. Operations without `operationId` are skipped as link sources.

---

## OpenApiMerge

`Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge`

| Type / Function | Signature | Description |
|-----------------|-----------|-------------|
| `MergeError` | `FileNotFound` \| `ParseError` \| `MergeConflict` | Merge failure cases |
| `mergeDocuments` | `OpenApiDocument list -> OpenApiDocument` | In-memory merge (first-wins) |
| `mergeFiles` | `string list -> Result<OpenApiDocument, MergeError>` | Load + merge files |

Merges: paths, all component dictionaries. Conflicting keys in target are skipped.

---

## OpenApiScissors

`Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors`

| Type / Function | Signature | Description |
|-----------------|-----------|-------------|
| `ScissorsOptions` | Record: `IncludeTags`, `IncludePaths`, `IncludeOperationIds`, `Transitive` | Filter config |
| `ScissorsOptions.Empty` | static | Default (`Transitive = true`) |
| `cutDocument` | `OpenApiDocument -> ScissorsOptions -> OpenApiDocument` | Filtered copy |

Filter logic: operation kept if **any** of tag, path substring, or operationId matches (when filters specified). `Transitive = true` copies referenced component schemas.

---

## OpenApiWriterTools

`Microsoft.OpenAPI.FunctionalExtensions.OpenApiWriterTools`

| Function | Signature | Description |
|----------|-----------|-------------|
| `saveDocument` | `OpenApiDocument -> string -> unit` | Write `.json` or `.yaml`/`.yml` via `SerializeAsV3` |

---

## GraphvizExport

`Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport`

| Function | Signature | Output |
|----------|-----------|--------|
| `exportSchemaGraphToSvg` | `OpenApiDocument -> string -> unit` | Full schema graph SVG |
| `exportSchemaGraphToDot` | `OpenApiDocument -> string -> unit` | Full schema graph DOT |
| `exportSingleComponentSchemaToSvg` | `OpenApiDocument -> string -> string -> unit` | Single component SVG |
| `exportRouteMapToSvg` | `OpenApiDocument -> string -> unit` | Hub + route chains (title as hub) |
| `exportRouteMapToSvgWith` | `OpenApiDocument -> string -> RouteSvgOptions -> unit` | Custom route diagram |
| `renderLinksGraph` | `LinksGraph -> string -> unit` | Links IR → SVG |
| `exportLinksGraphToSvg` | `OpenApiDocument -> string -> unit` | Collect links IR and render SVG |

### RouteSvgOptions

| Field | Type | Default in `exportRouteMapToSvg` |
|-------|------|----------------------------------|
| `CenterLabel` | `string option` | `doc.Info.Title` |
| `IncludeOperations` | `bool` | `false` |
| `IncludeSchemas` | `bool` | `false` |

---

## Microsoft.OpenAPI.FunctionalExtensions.Linting

Package: `Functional.Microsoft.OpenAPI.Extensions.Linting`

### Types (`Linting.Types`)

| Type | Cases / Fields |
|------|----------------|
| `Severity` | `Error` \| `Warning` \| `Info` |
| `RuleId` | `string` (type alias) |
| `LintLocation` | `DocumentLevel` \| `PathLevel` \| `OperationLevel` \| `SchemaLevel` \| `SchemaPropertyLevel` \| `ParameterLevel` |
| `LintViolation` | `Rule`, `Severity`, `Message`, `Location` |
| `LintResult` | `Violations: LintViolation list`, `DocumentPath: string option` |
| `LintRule` | `OpenApiDocument -> LintViolation list` |
| `NamedRule` | `Id: RuleId`, `Rule: LintRule` |

### Rules (`Linting.Rules`)

Built-in documentation and structure rules (`defaultNamedRules`):

| Rule ID | Severity | Function |
|---------|----------|----------|
| `missing-operation-id` | Error | `missingOperationId` |
| `duplicate-operation-id` | Error | `duplicateOperationId` |
| `path-parameter-not-defined` | Error | `pathParameterNotDefined` |
| `operation-without-responses` | Error | `operationWithoutResponses` |
| `duplicate-path-parameter` | Error | `duplicatePathParameter` |
| `empty-operation-summary` | Warning | `emptyOperationSummary` |
| `empty-parameter-description` | Warning | `emptyParameterDescription` |
| `empty-schema-property-description` | Warning | `emptySchemaPropertyDescription` |
| `unused-schemas` | Warning | `unusedSchemas` |
| `missing-response-description` | Error | `missingResponseDescription` |
| `path-without-operations` | Warning | `pathWithoutOperations` |
| `missing-content-type` | Error | `missingContentType` |

### ExampleValidation (`Linting.ExampleValidation`)

| Rule ID | Severity | Function |
|---------|----------|----------|
| `invalid-examples` | Error / Info | `invalidExamples` |

Validates inline and named examples against schemas (type, enum, required properties, formats).

### LinterConfig (`Linting.LinterConfig`)

| Member | Description |
|--------|-------------|
| `defaults` | All default rules enabled |
| `withOnly` | `RuleId list -> LinterConfig -> LinterConfig` — run only listed rules |
| `without` | `RuleId list -> LinterConfig -> LinterConfig` — exclude rules |
| `withCustom` | `LintRule list -> LinterConfig -> LinterConfig` — append custom rules |
| `withSeverity` | `RuleId -> Severity -> LinterConfig -> LinterConfig` — override severity |

`LinterConfig` record fields: `EnabledRules`, `DisabledRules`, `CustomRules`, `Severity`.

### Linter (`Linting.Linter`)

| Function | Signature | Description |
|----------|-----------|-------------|
| `defaultNamedRules` | `NamedRule list` | All 12 built-in rules (11 structure + `invalid-examples`) |
| `lint` | `LintRule list -> OpenApiDocument -> LintResult` | Run explicit rule list |
| `lintWithConfig` | `LinterConfig -> OpenApiDocument -> LintResult` | Run configured rules |
| `lintWithDefaults` | `OpenApiDocument -> LintResult` | Run all default rules |

---

## ResultEx

`ResultEx` — helpers over `FSharp.Core.Result`

| Function | Signature |
|----------|-----------|
| `map` | `('a -> 'b) -> Result<'a,'e> -> Result<'b,'e>` |
| `bind` | `('a -> Result<'b,'e>) -> Result<'a,'e> -> Result<'b,'e>` |
| `mapError` | `('e -> 'f) -> Result<'a,'e> -> Result<'a,'f>` |
| `bindError` | `('e -> Result<'a,'f>) -> Result<'a,'e> -> Result<'a,'f>` |
| `apply` | `Result<('a -> 'b),'e> -> Result<'a,'e> -> Result<'b,'e>` |
| `ofOption` | `'e -> 'a option -> Result<'a,'e>` |
| `toOption` | `Result<'a,'e> -> 'a option` |
| `tryCatch` | `('a -> 'b) -> (exn -> 'e) -> 'a -> Result<'b,'e>` |
| `sequence` | `seq<Result<'a,'e>> -> Result<seq<'a>,'e>` |
| `traverse` | `('a -> Result<'b,'e>) -> seq<'a> -> Result<seq<'b>,'e>` |
| `tee` / `teeError` | Side-effect helpers |

`AsyncResult` submodule: `ofAsync`, `map`, `bind`, `mapError`, `ofResult`, `toResult`.

---

## OpenApiSchemaAnalysis

Module `OpenApiSchemaAnalysis` (internal helper, also used by adapters).

| Type / Function | Description |
|-----------------|-------------|
| `SchemaClass` | `Array` \| `Reference` \| `Primitive` \| `Object` |
| `tryGetReferenceId` | `IOpenApiSchema -> string option` |

## OpenAPI.NET v2.0 Migration Notes

Key practical changes and tips collected while upgrading to `Microsoft.OpenApi` v2.0.0 and `Microsoft.OpenApi.YamlReader` v2.0.0.

### Model API Changes

- Prefer interfaces: `IOpenApiSchema`, `IOpenApiParameter`, `IOpenApiPathItem`, etc. Concrete types may be proxies/wrappers.
- `JsonSchemaType` is a flags enum. Nullability is expressed via `JsonSchemaType.Null` (no `Nullable` bool on schema).
- `Any`/extensions use `System.Text.Json.Nodes.JsonNode` and `JsonNodeExtension`. `OpenApiAny` types were removed.
- HTTP methods should be `System.Net.Http.HttpMethod` in our IR; when reading the document, operations are still keyed by the spec’s method map.
- References use `BaseOpenApiReference` and holder interfaces `IOpenApiReferenceHolder`, `IOpenApiReferenceHolder<T>`, `IOpenApiReferenceHolder<T,U,V>`. Concrete schema references are `OpenApiSchemaReference`.
  - `OpenApiSchemaReference.Id` may be empty. Use the underlying `BaseOpenApiReference.Id` or parse `ReferenceV3`/`ReferenceV2` (`#/components/schemas/Name`).
  - When necessary, resolve via `RecursiveTarget`/`Target` or use the document’s `Components.Schemas` map.

### Reading Documents

- Use `OpenApiDocument.Parse/Load/LoadAsync` instead of `OpenApiStringReader`.
- For YAML support, register reader: `OpenApiReaderSettingsExtensions.AddYamlReader(settings)` and pass `format = OpenApiConstants.Yaml` when appropriate.
- Diagnostics: `ReadResult.Diagnostic` may be null. Access errors via `diag.Errors` and map to our error model.

### Collections and Nulls

- Many collections can be null; use pattern matching to guard before enumeration.
- Use options instead of nulls internally; convert at the edges.

### Extensions and JsonNode

- Extract values from `JsonNode` using safe conversions; for non-string primitives, serialize or map to strings as needed.
- For `IOpenApiExtension` values, prefer `JsonNodeExtension.Node`. For non-JsonNode extensions, fallback to JSON string via `OpenApiJsonWriter` if needed.

### Traversal and IR

- Traverse compositions consistently: `allOf`, `oneOf`, `anyOf`.
- Handle arrays (`Items`) and maps (`AdditionalProperties`) carefully; both may be null.
- Avoid assuming concrete types; match on interfaces and downcast only when required by traversal helpers.

### Error Handling and ROP

- Convert exceptions at boundaries to `Result` with context; do not use exceptions for control flow.
- Public APIs should return `Result<'ok,'err>` or `Async<Result<'ok,'err>>`.

### Known Gotchas

- `OpenApiSchemaReference.Id` can be empty; prefer `BaseOpenApiReference.Id` or parse `ReferenceV3`/`ReferenceV2`.
- `JsonSchemaType` can include multiple flags (e.g., `String, Null`); strip `Null` when rendering ‘kind’ but surface nullability marker separately.
- `IOpenApi*` wrappers may proxy targets; `RecursiveTarget` can be used but may not be publicly accessible everywhere—have safe fallbacks.

### Examples

- Reading with YAML:

```fsharp
let settings = OpenApiReaderSettings()
OpenApiReaderSettingsExtensions.AddYamlReader(settings)
let rr = OpenApiDocument.Parse(content, format = OpenApiConstants.Yaml, settings = settings)
```

- Reference id extraction:

```fsharp
match schema with
| :? OpenApiSchemaReference as r when not (String.IsNullOrWhiteSpace r.Id) -> Some r.Id
| _ ->
    // try BaseOpenApiReference via reflection; fallback to parsing ReferenceV3
    // see OpenApiSchemaAnalysis.tryGetReferenceId
```



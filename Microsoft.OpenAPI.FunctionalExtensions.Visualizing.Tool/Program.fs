open System
open Argu
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport
open OpenApiTraversal
open OpenApiLinksTraversal
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiMerge
open System.IO
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiScissors
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiWriterTools
open Microsoft.OpenAPI.FunctionalExtensions.Linting

type SchemaArgs =
  | [<Mandatory>] Input of path:string
  | [<Mandatory>] Out of path:string
  | Component of string
  | Dot
with
  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Input _ -> "Path to OpenAPI spec (yaml/json)"
      | Out _ -> "Output SVG path"
      | Component _ -> "Component schema name to visualize (from #/components/schemas)"
      | Dot _ -> "Output DOT instead of SVG"

type RouteArgs =
  | [<Mandatory>] Input of path:string
  | [<Mandatory>] Out of path:string
  | Center of string
  | Include_Operations
  | Include_Schemas
with
  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Input _ -> "Path to OpenAPI spec (yaml/json)"
      | Out _ -> "Output SVG path"
      | Center _ -> "Center hub label"
      | Include_Operations -> "Include per-operation nodes"
      | Include_Schemas -> "Include referenced schema nodes"

type LintArgs =
  | [<Mandatory>] Input of path:string
  | Disable_Rule of ruleId:string
with
  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Input _ -> "Path to OpenAPI spec (yaml/json)"
      | Disable_Rule _ -> "Disable a lint rule by ID (repeatable)"

type CliArgs =
  | [<CliPrefix(CliPrefix.DoubleDash)>] Lint of ParseResults<LintArgs>
  | [<CliPrefix(CliPrefix.DoubleDash)>] Schema_Svg of ParseResults<SchemaArgs>
  | [<CliPrefix(CliPrefix.DoubleDash)>] Route_Svg of ParseResults<RouteArgs>
  | [<CliPrefix(CliPrefix.DoubleDash)>] Links_Svg of ParseResults<CollectArgs>
  | [<CliPrefix(CliPrefix.DoubleDash)>] Merge of ParseResults<MergeArgs>
  | [<CliPrefix(CliPrefix.DoubleDash)>] Schema_Collect of ParseResults<CollectArgs>
  | [<CliPrefix(CliPrefix.DoubleDash)>] Route_Collect of ParseResults<CollectArgs>
  | [<CliPrefix(CliPrefix.DoubleDash)>] Links_Collect of ParseResults<CollectArgs>
  | [<CliPrefix(CliPrefix.DoubleDash)>] Scissors of ParseResults<ScissorsArgs>
with
  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Lint _ -> "Lint an OpenAPI specification"
      | Schema_Svg _ -> "Render schema graph to SVG"
      | Route_Svg _ -> "Render route map to SVG"
      | Links_Svg _ -> "Render links graph to SVG"
      | Merge _ -> "Merge multiple OpenAPI specs into one"
      | Schema_Collect _ -> "Collect Schema Graph IR as JSON"
      | Route_Collect _ -> "Collect Route Map IR as JSON"
      | Links_Collect _ -> "Collect LinksGraph IR as JSON"
      | Scissors _ -> "Cut subset of spec by tags/paths/operations"

and MergeArgs =
  | Input of path:string
  | [<Mandatory>] Out of path:string
with
  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Input _ -> "Input spec path (yaml/json). Repeat to add multiple inputs"
      | Out _ -> "Output spec path (.yaml/.yml or .json)"

and CollectArgs =
  | [<Mandatory>] Input of path:string
  | [<Mandatory>] Out of path:string
with
  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Input _ -> "Path to OpenAPI spec (yaml/json)"
      | Out _ -> "Output JSON path"

and ScissorsArgs =
  | [<Mandatory>] Input of path:string
  | [<Mandatory>] Out of path:string
  | Include_Tag of tag:string
  | Include_Path of path:string
  | Include_Operation of opId:string
  | No_Transitive
with
  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Input _ -> "Input spec path"
      | Out _ -> "Output spec path"
      | Include_Tag _ -> "Include operations with this tag (repeatable)"
      | Include_Path _ -> "Include routes that contain this substring (repeatable)"
      | Include_Operation _ -> "Include operations by id (repeatable)"
      | No_Transitive _ -> "Do not include transitive component schemas"

let private formatSeverity (severity: Types.Severity) =
  match severity with
  | Types.Error -> "ERROR"
  | Types.Warning -> "WARNING"
  | Types.Info -> "INFO"

let private formatLocation (location: Types.LintLocation) =
  match location with
  | Types.DocumentLevel -> "document"
  | Types.PathLevel path -> $"path:{path}"
  | Types.OperationLevel (path, method, operationId) ->
      match operationId with
      | Some id -> $"operation:{method} {path} ({id})"
      | None -> $"operation:{method} {path}"
  | Types.SchemaLevel schemaName -> $"schema:{schemaName}"
  | Types.SchemaPropertyLevel (schemaName, propertyName) -> $"schema:{schemaName}.{propertyName}"
  | Types.ParameterLevel (path, method, parameterName) -> $"parameter:{method} {path}/{parameterName}"

let private printViolation (violation: Types.LintViolation) =
  printfn
    "[%s] %s: %s (%s)"
    (formatSeverity violation.Severity)
    (formatLocation violation.Location)
    violation.Message
    violation.Rule

[<EntryPoint>]
let main argv =
  let parser = ArgumentParser.Create<CliArgs>(programName = "openapi-fx")
  let results = parser.ParseCommandLine argv
  match results.GetSubCommand() with
  | Lint lintArgs ->
      let input = lintArgs.GetResult(<@ LintArgs.Input @>)
      let disabledRules = lintArgs.GetResults(<@ LintArgs.Disable_Rule @>) |> List.ofSeq
      match readSpecification input with
      | Error e -> eprintfn "%A" e; 2
      | Ok doc ->
          let config =
            LinterConfig.defaults
            |> LinterConfig.without disabledRules

          let result = Linter.lintWithConfig config doc

          for violation in result.Violations do
            printViolation violation

          if result.Violations |> List.exists (fun violation -> violation.Severity = Types.Error) then
            1
          else
            0
  | Schema_Svg schema ->
      let input = schema.GetResult(<@ SchemaArgs.Input @>)
      let outp = schema.GetResult(<@ SchemaArgs.Out @>)
      let comp = schema.TryGetResult(<@ SchemaArgs.Component @>)
      let asDot = schema.Contains(<@ SchemaArgs.Dot @>)
      match readSpecification input with
      | Ok doc ->
          match comp, asDot with
          | Some name, false -> exportSingleComponentSchemaToSvg doc name outp; 0
          | None, false -> exportSchemaGraphToSvg doc outp; 0
          | _, true -> exportSchemaGraphToDot doc outp; 0
      | Error e -> eprintfn "%A" e; 2
  | Links_Svg links ->
      let input = links.GetResult(<@ CollectArgs.Input @>)
      let outp = links.GetResult(<@ CollectArgs.Out @>)
      match readSpecification input with
      | Ok doc ->
          exportLinksGraphToSvg doc outp
          0
      | Error e -> eprintfn "%A" e; 2
  | Route_Svg route ->
      let input = route.GetResult(<@ RouteArgs.Input @>)
      let outp = route.GetResult(<@ RouteArgs.Out @>)
      let center = route.TryGetResult(<@ RouteArgs.Center @>)
      let includeOps = route.Contains(<@ RouteArgs.Include_Operations @>)
      let includeSchemas = route.Contains(<@ RouteArgs.Include_Schemas @>)
      match readSpecification input with
      | Ok doc ->
          exportRouteMapToSvgWith doc outp { CenterLabel = center; IncludeOperations = includeOps; IncludeSchemas = includeSchemas }
          0
      | Error e -> eprintfn "%A" e; 2
  | Merge mergeArgs ->
      let inputs = mergeArgs.GetResults(<@ MergeArgs.Input @>)
      let outp = mergeArgs.GetResult(<@ MergeArgs.Out @>)
      match mergeFiles (inputs |> List.ofSeq) with
      | Error e -> eprintfn "%A" e; 2
      | Ok doc ->
          saveDocument doc outp
          0
  | Schema_Collect args ->
      let input = args.GetResult(<@ CollectArgs.Input @>)
      let outp = args.GetResult(<@ CollectArgs.Out @>)
      match readSpecification input with
      | Error e -> eprintfn "%A" e; 2
      | Ok doc ->
          let g = collectDocumentSchemas doc
          use fs = new FileStream(outp, FileMode.Create, FileAccess.Write, FileShare.None)
          use jw = new System.Text.Json.Utf8JsonWriter(fs, System.Text.Json.JsonWriterOptions(Indented = true))
          jw.WriteStartObject()
          jw.WritePropertyName("nodes"); jw.WriteStartArray()
          for n in g.Nodes do
            jw.WriteStartObject()
            jw.WriteString("id", n.Id)
            match n.Title with | Some v -> jw.WriteString("title", v) | None -> ()
            match n.Kind with | Some v -> jw.WriteString("kind", v) | None -> ()
            match n.Format with | Some v -> jw.WriteString("format", v) | None -> ()
            match n.ReadOnly with | Some v -> jw.WriteBoolean("readOnly", v) | None -> ()
            match n.WriteOnly with | Some v -> jw.WriteBoolean("writeOnly", v) | None -> ()
            match n.EnumValues with
            | Some vs ->
                jw.WritePropertyName("enum");
                jw.WriteStartArray();
                for v in vs do jw.WriteStringValue(v)
                jw.WriteEndArray()
            | None -> ()
            jw.WriteEndObject()
          jw.WriteEndArray()
          jw.WritePropertyName("edges"); jw.WriteStartArray()
          for e in g.Edges do
            jw.WriteStartObject()
            jw.WriteString("from", e.FromId)
            jw.WriteString("to", e.ToId)
            match e.EdgeKind with
            | Property name -> jw.WriteString("kind", "property"); jw.WriteString("name", name)
            | ArrayItem -> jw.WriteString("kind", "arrayItem")
            | MapValue -> jw.WriteString("kind", "mapValue")
            | Composition ck ->
                jw.WriteString("kind", "composition");
                let v = match ck with | AllOf -> "allOf" | OneOf -> "oneOf" | AnyOf -> "anyOf"
                jw.WriteString("type", v)
            jw.WriteEndObject()
          jw.WriteEndArray()
          jw.WriteEndObject(); jw.Flush(); 0
  | Links_Collect args ->
      let input = args.GetResult(<@ CollectArgs.Input @>)
      let outp = args.GetResult(<@ CollectArgs.Out @>)
      match readSpecification input with
      | Error e -> eprintfn "%A" e; 2
      | Ok doc ->
          let graph = collectLinksGraph doc
          use fs = new FileStream(outp, FileMode.Create, FileAccess.Write, FileShare.None)
          use jw = new System.Text.Json.Utf8JsonWriter(fs, System.Text.Json.JsonWriterOptions(Indented = true))
          jw.WriteStartObject()
          jw.WritePropertyName("operations")
          jw.WriteStartArray()
          for operationId in graph.Operations do
            jw.WriteStringValue(operationId)
          jw.WriteEndArray()
          jw.WritePropertyName("links")
          jw.WriteStartArray()
          for link in graph.Links do
            jw.WriteStartObject()
            jw.WriteString("linkName", link.LinkName)
            jw.WriteString("sourceOperationId", link.SourceOperationId)
            jw.WriteString("targetOperationId", link.TargetOperationId)
            match link.Description with
            | Some description -> jw.WriteString("description", description)
            | None -> ()
            match link.Source with
            | ResponseBody pointer ->
                jw.WriteString("sourceKind", "responseBody")
                jw.WriteString("sourcePointer", pointer)
            | ResponseHeader headerName ->
                jw.WriteString("sourceKind", "responseHeader")
                jw.WriteString("sourceHeader", headerName)
            | RequestBody pointer ->
                jw.WriteString("sourceKind", "requestBody")
                jw.WriteString("sourcePointer", pointer)
            match link.Target with
            | OperationParameter parameterName ->
                jw.WriteString("targetKind", "operationParameter")
                jw.WriteString("targetParameter", parameterName)
            | RequestBodyField pointer ->
                jw.WriteString("targetKind", "requestBodyField")
                jw.WriteString("targetPointer", pointer)
            jw.WriteEndObject()
          jw.WriteEndArray()
          jw.WriteEndObject()
          jw.Flush()
          0
  | Route_Collect args ->
      let input = args.GetResult(<@ CollectArgs.Input @>)
      let outp = args.GetResult(<@ CollectArgs.Out @>)
      match readSpecification input with
      | Error e -> eprintfn "%A" e; 2
      | Ok doc ->
          let m = OpenApiOperationsTraversal.collectRouteMap doc
          use fs = new FileStream(outp, FileMode.Create, FileAccess.Write, FileShare.None)
          use jw = new System.Text.Json.Utf8JsonWriter(fs, System.Text.Json.JsonWriterOptions(Indented = true))
          jw.WriteStartObject()
          jw.WritePropertyName("routes"); jw.WriteStartArray()
          for r in m.Routes do
            jw.WriteStartObject()
            jw.WriteString("path", r.Path)
            jw.WriteString("method", r.Method)
            match r.OperationId with | Some v -> jw.WriteString("operationId", v) | None -> ()
            jw.WritePropertyName("tags");
            jw.WriteStartArray(); for t in r.Tags do jw.WriteStringValue(t); jw.WriteEndArray()
            jw.WritePropertyName("parameterSchemas");
            jw.WriteStartArray(); for s in r.ParameterSchemas do jw.WriteStringValue(s); jw.WriteEndArray()
            jw.WritePropertyName("requestSchemas");
            jw.WriteStartArray(); for s in r.RequestSchemas do jw.WriteStringValue(s); jw.WriteEndArray()
            jw.WritePropertyName("responseSchemas");
            jw.WriteStartArray(); for s in r.ResponseSchemas do jw.WriteStringValue(s); jw.WriteEndArray()
            jw.WriteBoolean("returnsArray", r.ReturnsArray)
            jw.WriteBoolean("returnsArrayViaData", r.ReturnsArrayViaData)
            jw.WriteEndObject()
          jw.WriteEndArray(); jw.WriteEndObject(); jw.Flush(); 0
  | Scissors sargs ->
      let input = sargs.GetResult(<@ ScissorsArgs.Input @>)
      let outp = sargs.GetResult(<@ ScissorsArgs.Out @>)
      let tags = sargs.GetResults(<@ ScissorsArgs.Include_Tag @>) |> List.ofSeq
      let paths = sargs.GetResults(<@ ScissorsArgs.Include_Path @>) |> List.ofSeq
      let ops = sargs.GetResults(<@ ScissorsArgs.Include_Operation @>) |> List.ofSeq
      let trans = not (sargs.Contains(<@ ScissorsArgs.No_Transitive @>))
      match readSpecification input with
      | Error e -> eprintfn "%A" e; 2
      | Ok doc ->
          let opts: ScissorsOptions = { ScissorsOptions.Empty with IncludeTags = tags; IncludePaths = paths; IncludeOperationIds = ops; Transitive = trans }
          let cut = cutDocument doc opts
          saveDocument cut outp
          0
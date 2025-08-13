open System
open Argu
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport

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

type CliArgs =
  | [<CliPrefix(CliPrefix.DoubleDash)>] Schema_Svg of ParseResults<SchemaArgs>
  | [<CliPrefix(CliPrefix.DoubleDash)>] Route_Svg of ParseResults<RouteArgs>
with
  interface IArgParserTemplate with
    member s.Usage =
      match s with
      | Schema_Svg _ -> "Render schema graph to SVG"
      | Route_Svg _ -> "Render route map to SVG"

[<EntryPoint>]
let main argv =
  let parser = ArgumentParser.Create<CliArgs>(programName = "openapi-visualizer")
  let results = parser.ParseCommandLine argv
  match results.GetSubCommand() with
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
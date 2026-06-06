module Microsoft.OpenAPI.FunctionalExtensions.Visualizing.GraphvizExport

open Rubjerg.Graphviz
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.Visualizing.SchemaGraph
open OpenApiOperationsTraversal

let private safeId (s: string) =
  s.Replace("/", "_").Replace("#", "_").Replace("-", "_")

let private prettySchemaLabel (id: string) =
  if System.String.IsNullOrWhiteSpace(id) then id else
  // Try to collapse component schema pointers and property/items segments into readable labels
  let parts = id.Split('/')
  match id.StartsWith("#/components/schemas/") with
  | true -> parts |> Array.tryLast |> Option.defaultValue id
  | false ->
      match id.Contains("/properties/") with
      | true ->
          let i = System.Array.IndexOf(parts, "properties")
          if i >= 0 && i+1 < parts.Length then parts[i+1] else id
      | false -> if id.EndsWith("/items") then "items" else id

let private tryFindNode (g: SchemaGraph) (id: string) =
  g.Nodes |> Seq.tryFind (fun n -> n.Id = id)

let private hasNullFlag (kindOpt: string option) =
  match kindOpt with
  | Some k when k.IndexOf("Null", System.StringComparison.OrdinalIgnoreCase) >= 0 -> true
  | _ -> false

let private cleanKind (kindOpt: string option) =
  match kindOpt with
  | None -> None
  | Some k ->
      let parts = k.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.filter (fun s -> not (s.Equals("Null", System.StringComparison.OrdinalIgnoreCase)))
      if parts.Length = 0 then Some "unknown" else Some (System.String.Join(", ", parts))

let private nodeDecorations (nOpt: SchemaNode option) =
  match nOpt with
  | None -> None
  | Some n ->
      // format уже включается в computeTypeLabel; здесь только флаги и enum
      let ro = match n.ReadOnly with Some true -> " [readonly]" | _ -> ""
      let wo = match n.WriteOnly with Some true -> " [writeonly]" | _ -> ""
      let en =
        match n.EnumValues with
        | Some vs when vs.Length > 0 -> sprintf " enum{%s}" (System.String.Join("|", vs))
        | _ -> ""
      let composed = (ro + wo + en).Trim()
      if System.String.IsNullOrWhiteSpace composed then None else Some composed

let rec private computeTypeLabel (g: SchemaGraph) (id: string) : string =
  let nOpt = tryFindNode g id
  let hasArrayEdge = g.Edges |> Seq.exists (fun e -> e.FromId = id && (match e.EdgeKind with | ArrayItem -> true | _ -> false))
  let isArrayNode =
    hasArrayEdge || (nOpt |> Option.bind (fun n -> n.Kind) |> Option.exists (fun k -> k.IndexOf("Array", System.StringComparison.OrdinalIgnoreCase) >= 0))
  let nullableMark = if nOpt |> Option.exists (fun n -> hasNullFlag n.Kind) then "?" else ""
  if isArrayNode then
    // find first ArrayItem edge and compute child type
    let childOpt = g.Edges |> Seq.tryFind (fun e -> e.FromId = id && match e.EdgeKind with | ArrayItem -> true | _ -> false) |> Option.map (fun e -> e.ToId)
    match childOpt with
    | Some childId -> sprintf "array[%s]%s" (computeTypeLabel g childId) nullableMark
    | None -> "array" + nullableMark
  else
    match nOpt with
    | None -> prettySchemaLabel id
    | Some n ->
        // если это компонент-схема и тип Object — показываем имя компонента
        let isComponent = n.Id.StartsWith("#/components/schemas/")
        let kindRaw = cleanKind n.Kind |> Option.defaultValue ""
        let preferName = isComponent && (System.String.IsNullOrWhiteSpace kindRaw || kindRaw.Equals("Object", System.StringComparison.OrdinalIgnoreCase))
        if preferName then prettySchemaLabel n.Id + nullableMark
        else
          let kind = if System.String.IsNullOrWhiteSpace kindRaw then prettySchemaLabel n.Id else kindRaw
          match n.Format with
          | Some f when not (System.String.IsNullOrWhiteSpace f) -> sprintf "%s/%s%s" kind f nullableMark
          | _ -> kind + nullableMark

let exportSchemaGraphToSvg (doc: OpenApiDocument) (outPath: string) =
  let g = collectDocumentSchemas doc
  let root = RootGraph.CreateNew(GraphType.Directed, "schemas")
  // nodes with labels; for root component nodes show the component name instead of '#'
  for n in g.Nodes do
    let nodeId = safeId n.Id
    let node = root.GetOrAddNode(nodeId)
    let label =
      if n.Id.StartsWith("#/components/schemas/") then prettySchemaLabel n.Id else prettySchemaLabel n.Id
    node.SetAttribute("label", label) |> ignore
  // edges (no edge labels to reduce clutter)
  for e in g.Edges do
    let fromN = root.GetOrAddNode(safeId e.FromId)
    let toN = root.GetOrAddNode(safeId e.ToId)
    root.GetOrAddEdge(fromN, toN, null) |> ignore

  // use property names as child node labels with types when possible
  for e in g.Edges do
    match e.EdgeKind with
    | Property name ->
        let child = root.GetOrAddNode(safeId e.ToId)
        // enrich label: two lines (name, then type in parentheses + extras)
        let nodeInfoOpt = tryFindNode g e.ToId
        let typeText = computeTypeLabel g e.ToId
        let extras = nodeDecorations nodeInfoOpt |> Option.map (fun s -> " " + s) |> Option.defaultValue ""
        let label = sprintf "%s\n(%s%s)" name typeText extras
        child.SetAttribute("label", label) |> ignore
    | ArrayItem ->
        // draw edge from the array schema to its item schema; also label the 'items' node with array element type
        let arrayNode = root.GetOrAddNode(safeId e.FromId)
        let itemNode = root.GetOrAddNode(safeId e.ToId)
        let name = prettySchemaLabel e.ToId
        let typeText = computeTypeLabel g e.FromId // parent schema describes the array itself
        let label = sprintf "%s\n(%s)" name typeText
        itemNode.SetAttribute("label", label) |> ignore
        root.GetOrAddEdge(arrayNode, itemNode, null) |> ignore
    | _ -> ()
  ()
  root.ToSvgFile(outPath)

let exportSchemaGraphToDot (doc: OpenApiDocument) (outPath: string) =
  let g = collectDocumentSchemas doc
  let sb = System.Text.StringBuilder()
  let append (s:string) = sb.AppendLine(s) |> ignore
  let esc (s:string) = s.Replace("\\", "\\\\").Replace("\"", "\\\"")
  let nid (id:string) = safeId id
  append "digraph schemas {"
  // initial node labels (will refine below)
  for n in g.Nodes do
    let label = prettySchemaLabel n.Id
    append (sprintf "  %s [label=\"%s\"];" (nid n.Id) (esc label))
  // edges
  for e in g.Edges do
    append (sprintf "  %s -> %s;" (nid e.FromId) (nid e.ToId))
  // refine labels for properties and arrays
  for e in g.Edges do
    match e.EdgeKind with
    | Property name ->
        let typeText = computeTypeLabel g e.ToId
        let extras = nodeDecorations (tryFindNode g e.ToId) |> Option.defaultValue ""
        let label = sprintf "%s\\n(%s%s)" name (esc typeText) (esc extras)
        append (sprintf "  %s [label=\"%s\"];" (nid e.ToId) label)
    | ArrayItem ->
        // label the 'items' node with array[...] derived from parent
        let label = sprintf "items\\n(%s)" (esc (computeTypeLabel g e.FromId))
        append (sprintf "  %s [label=\"%s\"];" (nid e.ToId) label)
    | _ -> ()
  append "}"
  System.IO.File.WriteAllText(outPath, sb.ToString())

let exportSingleComponentSchemaToSvg (doc: OpenApiDocument) (componentName: string) (outPath: string) =
  let rootGraph = RootGraph.CreateNew(GraphType.Directed, "schema")
  if isNull doc.Components || isNull doc.Components.Schemas then
    rootGraph.ToSvgFile(outPath)
  else
    match doc.Components.Schemas.TryGetValue componentName with
    | true, s ->
        // collect subgraph for this schema only
        let sub = collectSchemaGraph s
        // root node as component name
        let rootNode = rootGraph.GetOrAddNode(safeId componentName)
        rootNode.SetAttribute("label", componentName) |> ignore
        // map nodes/edges under this root
        for e in sub.Edges do
          match e.EdgeKind with
          | Property name when e.FromId = "#" || e.FromId.Contains("/components/schemas/") ->
              let child = rootGraph.GetOrAddNode(safeId e.ToId)
              let typeText = computeTypeLabel sub e.ToId
              let extras = nodeDecorations (tryFindNode sub e.ToId) |> Option.map (fun s -> " " + s) |> Option.defaultValue ""
              let label = sprintf "%s\n(%s%s)" name typeText extras
              child.SetAttribute("label", label) |> ignore
              rootGraph.GetOrAddEdge(rootNode, child, null) |> ignore
          | ArrayItem ->
              let arrayNode = rootGraph.GetOrAddNode(safeId e.FromId)
              let itemNode = rootGraph.GetOrAddNode(safeId e.ToId)
              rootGraph.GetOrAddEdge(arrayNode, itemNode, null) |> ignore
          | _ -> ()
        rootGraph.ToSvgFile(outPath)
    | _ -> rootGraph.ToSvgFile(outPath)

type RouteSvgOptions = { CenterLabel: string option; IncludeOperations: bool; IncludeSchemas: bool }

let exportRouteMapToSvgWith (doc: OpenApiDocument) (outPath: string) (opts: RouteSvgOptions) =
  let m = collectRouteMap doc
  let root = RootGraph.CreateNew(GraphType.Directed, "routes")
  // numbering option (future: could be driven by CLI)
  let mutable counter = 0
  let nextId () = counter <- counter + 1; counter
  // central hub
  let hubOpt =
    match opts.CenterLabel with
    | Some label ->
        let n = root.GetOrAddNode(safeId label)
        n.SetAttribute("label", label) |> ignore
        Some n
    | None -> None

  // helpers to split path into chain: /pets -> node '/pets', '/pets/{petId}' -> '/pets' -> '{petId}'
  let ensurePathChain (path: string) =
    let segments = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries)
    let mutable parentOpt = hubOpt
    let mutable lastNode = Unchecked.defaultof<Node>
    for i = 0 to segments.Length - 1 do
      let seg = segments[i]
      let label = if seg.StartsWith("{") then seg else "/" + seg
      let node = root.GetOrAddNode(safeId (if seg.StartsWith("{") then seg else "/" + seg))
      node.SetAttribute("label", label) |> ignore
      match parentOpt with
      | Some p -> root.GetOrAddEdge(p, node, null) |> ignore
      | None -> ()
      parentOpt <- Some node
      lastNode <- node
    lastNode

  // group by path and render chains
  let byPath = m.Routes |> Seq.groupBy (fun r -> r.Path) |> Seq.toList
  for (path, routes) in byPath do
    let leafNode = ensurePathChain path

    if opts.IncludeOperations then
      for r in routes do
        let opId = r.OperationId |> Option.defaultValue (sprintf "op%d" (nextId()))
        let opNode = root.GetOrAddNode(safeId (sprintf "%s_%s" r.Method opId))
        opNode.SetAttribute("label", r.Method) |> ignore
        root.GetOrAddEdge(leafNode, opNode, null) |> ignore
        if opts.IncludeSchemas then
          let link (sid:string) =
            let sn = root.GetOrAddNode(safeId sid)
            sn.SetAttribute("label", prettySchemaLabel sid) |> ignore
            root.GetOrAddEdge(opNode, sn, null) |> ignore
          r.ParameterSchemas |> Seq.iter link
          r.RequestSchemas |> Seq.iter link
          r.ResponseSchemas |> Seq.iter link
    else if opts.IncludeSchemas then
      // link schemas directly to route
      let schemas =
        routes |> Seq.collect (fun r -> Seq.concat [ r.ParameterSchemas; r.RequestSchemas; r.ResponseSchemas ])
        |> Seq.distinct
      for sid in schemas do
        let sn = root.GetOrAddNode(safeId sid)
        sn.SetAttribute("label", prettySchemaLabel sid) |> ignore
        root.GetOrAddEdge(leafNode, sn, null) |> ignore

  root.ToSvgFile(outPath)

let exportRouteMapToSvg (doc: OpenApiDocument) (outPath: string) =
  let title = if isNull doc.Info || System.String.IsNullOrWhiteSpace doc.Info.Title then None else Some doc.Info.Title
  exportRouteMapToSvgWith doc outPath { CenterLabel = title; IncludeOperations = false; IncludeSchemas = false }



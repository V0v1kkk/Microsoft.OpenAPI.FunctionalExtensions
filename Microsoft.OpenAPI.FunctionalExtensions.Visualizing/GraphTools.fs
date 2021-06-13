module GraphTools

open System
open System.Collections.Generic
open Shields.GraphViz
open Shields.GraphViz.Models
open Results
open Microsoft.OpenApi

let inline (!>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

let toNodeId string = string |> Id |> NodeId  


let private schemaNodeStyle =
    let mutable dic = System.Collections.Immutable.ImmutableDictionary.CreateBuilder<Id, Id>()
    dic.Add (Id("shape"),Id("circle"))
    dic.ToImmutable

let private propertyNodeStyle =
    let mutable dic = System.Collections.Immutable.ImmutableDictionary.CreateBuilder<Id, Id>()
    dic.Add (Id("shape"),Id("box"))
    dic.ToImmutable
    
    
let private aggregationEdgeStyle =
    let mutable dic = System.Collections.Immutable.ImmutableDictionary.CreateBuilder<Id, Id>()
    dic.Add (Id("dir"),Id("back"))
    dic.Add (Id("arrowtail"),Id("odiamond"))
    dic.ToImmutable

     
let addEdge (graph:Graph) (source:NodeId) (target:NodeId) style =
    EdgeStatement(source, target, style)
    |> graph.Add
     
let addSimpleEge (graph:Graph) (source:NodeId) (target:NodeId) =
    let emptyStyle = System.Collections.Immutable.ImmutableDictionary.Empty 
    addEdge graph source target emptyStyle
     
let addAggregationEdge (graph:Graph) (source:NodeId) (target:NodeId) =
    addEdge graph source target (aggregationEdgeStyle())
    
let addNode (graph:Graph) (label:Id) style =
    NodeStatement(label, style)
    |> graph.Add
    
let addSchemaNode (graph:Graph) (label:Id) =
    addNode graph label (schemaNodeStyle())
    
let addPropertyNode (graph:Graph) (label:Id) =
    addNode graph label (propertyNodeStyle())
    
let createGraph (baseSchemaName:String) (document:Models.OpenApiDocument) =
  let graph = Graph.Undirected
  //let addSimpleEge = GraphTools.addSimpleEge graph
  //let addAggregationEdge = GraphTools.addAggregationEdge graph
  //let addSchemaNode = GraphTools.addSchemaNode graph    
  //let addPropertyNode = GraphTools.addPropertyNode graph
  
  let schemaToGraph (graph,schema:KeyValuePair<String, Models.OpenApiSchema>) =  
    if schema.Value.AllOf.Count = 1
    then schema.Value.Properties <- (schema.Value.AllOf.Item 0).Properties
    (schema.Key |> Id |> addSchemaNode graph, schema.Value.Properties)
    
  let propertyToGraph (graph,property:KeyValuePair<String, Models.OpenApiSchema>) =
    let propertyName = property.Key
    let propertyType = property.Value.Reference.Id
    let caption = propertyName + " (" + propertyType + ")"
    (caption |> Id |> addPropertyNode graph, property.Value.Properties)
  
  let schema = getSchemaByName document baseSchemaName
  
  let rec writeRec (graph,schema:KeyValuePair<String, Models.OpenApiSchema>) =
    (graph,schema)
    |> schemaToGraph
    |> fun (graph, properties) -> Seq.fold (fun g p -> propertyToGraph) 
  
  //schema
  //>>= (fun s -> s.)
  
  //schema
  //>>= write
  //schema

  Success 0
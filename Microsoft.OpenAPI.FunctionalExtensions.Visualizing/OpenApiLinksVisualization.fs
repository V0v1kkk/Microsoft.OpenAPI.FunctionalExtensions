module OpenApiLinksVisualization

open Microsoft.OpenApi.Models
open Microsoft.OpenApi.Models
open Shields.GraphViz.Models
open OpenApiTools;

// in the first iteration only property from response can be related with another operation parameter
type GraphWithoutRepeats = GraphWithoutRepeats of Graph

type LinkName = LinkName of string

type SourceProperty = SourceProperty of string
type Source =
    | ResponseSource of SourceProperty
    // todo: add another sources

type ParameterName = ParameterName of string
type BodyPropertyName = BodyPropertyName of string

type TargetLocation =
    | Parameter of ParameterName
    | Body of BodyPropertyName

//type TargetParameter = TargetParameter of string




type UnvalidatedLinkInformation = { Name              : LinkName
                                    SourceOperationId : string
                                    TargetOperationId : string
                                    Source            : Source
                                    Target            : TargetLocation }

type ValidatedLinkInformation =   { Name              : LinkName
                                    SourceOperationId : string
                                    TargetOperationId : string
                                    Source            : Source
                                    Target            : TargetLocation }



type ErrorReason =
    | TargetParameterIsNotFound
    | BodyIsMissing

type LinkError =  { SourceOperationId : string
                    TargetOperationId : string
                    SourceProperty    : SourceProperty
                    Target            : TargetLocation
                    ErrorReason       : ErrorReason  }

type LinksErrors = LinkError seq

type GraphWithAnalytics = { GraphWithoutRepeats: GraphWithoutRepeats }

type ValidateLinks = UnvalidatedLinkInformation seq -> ValidatedLinkInformation seq * LinkError seq

type CreateGraphWithAnalytics = OpenApiDocument -> Graph

type ExtractLinks = OpenApiDocument -> UnvalidatedLinkInformation seq

type ExtractSourceFromLink = OpenApiLink -> Source 
type ExtractTargetFormLink = OpenApiLink -> TargetLocation

type ExtractLinkInformation = OpenApiOperation -> OpenApiResponse -> LinkName -> OpenApiLink -> UnvalidatedLinkInformation


let ExtractSourceFromLink:ExtractSourceFromLink = fun link ->
    link.Description |> SourceProperty |> ResponseSource // todo: implement

let extractLinkInformation: ExtractLinkInformation = fun operation response linkName link ->
    { Name = linkName
      SourceOperationId = operation.OperationId
      TargetOperationId = link.OperationId
      Source = ExtractSourceFromLink link
      Target = failwith "todo" } //todo: implement

let extractLinks:ExtractLinks = fun document ->
    document.Paths
  |> Seq.collect (fun path  -> path.Value.Operations.Values)
  |> Seq.map (fun operation -> (extractLinkInformation operation, seq operation.Responses.Values))
  |> Seq.collect (fun extractFunAndResponses  ->
      let (extractFun, responses) = extractFunAndResponses
      responses
      |> Seq.map (fun response -> (extractFun response, seq response.Links)))
  |> Seq.collect (fun extractFunAndLinks ->
      let (extractFun, links) = extractFunAndLinks
      links
      |> Seq.map (fun linkWithName -> (extractFun (LinkName linkWithName.Key) linkWithName.Value)))
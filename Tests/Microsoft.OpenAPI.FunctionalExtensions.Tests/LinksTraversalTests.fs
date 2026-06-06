module Microsoft.OpenAPI.FunctionalExtensions.Tests.LinksTraversal

open NUnit.Framework
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiLinksTraversal

[<Test>]
let ``Collect links graph from links example`` () =
    match readSpecification "Specifications/links-example.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectLinksGraph doc
        Assert.That(graph.Links.Length, Is.EqualTo(3))
        Assert.That(graph.Operations, Is.EqualTo<string list>([ "createUser"; "getOrder"; "getUser"; "getUserOrders" ]))

[<Test>]
let ``Link source and target expressions are parsed correctly`` () =
    match readSpecification "Specifications/links-example.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectLinksGraph doc

        let getUserOrdersLink =
            graph.Links |> List.find (fun link -> link.LinkName = "GetUserOrders")

        Assert.That(getUserOrdersLink.SourceOperationId, Is.EqualTo("getUser"))
        Assert.That(getUserOrdersLink.TargetOperationId, Is.EqualTo("getUserOrders"))
        Assert.That(getUserOrdersLink.Source, Is.EqualTo(ResponseBody "id"))
        Assert.That(getUserOrdersLink.Target, Is.EqualTo(OperationParameter "userId"))

        let createUserLink =
            graph.Links |> List.find (fun link -> link.LinkName = "GetCreatedUser")

        Assert.That(createUserLink.Source, Is.EqualTo(ResponseHeader "Location"))
        Assert.That(createUserLink.Target, Is.EqualTo(OperationParameter "userId"))

        let ordersLink =
            graph.Links |> List.find (fun link -> link.LinkName = "GetFirstOrder")

        Assert.That(ordersLink.Source, Is.EqualTo(ResponseBody "0/orderId"))
        Assert.That(ordersLink.Target, Is.EqualTo(OperationParameter "orderId"))

[<Test>]
let ``Operations without links are excluded unless they are link targets`` () =
    match readSpecification "Specifications/links-example.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectLinksGraph doc
        Assert.That(graph.Operations |> List.contains "listUsers", Is.False)
        Assert.That(graph.Operations |> List.contains "getOrder", Is.True)

[<Test>]
let ``Document with no links returns empty links graph`` () =
    let yaml =
        """
openapi: 3.0.3
info:
  title: Empty Links
  version: 1.0.0
paths:
  /ping:
    get:
      operationId: ping
      responses:
        '200':
          description: ok
"""

    match Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools.convertToSpecification yaml with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectLinksGraph doc
        Assert.That(graph.Links, Is.Empty)
        Assert.That(graph.Operations, Is.Empty)

[<Test>]
let ``Petstore returns empty links graph`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectLinksGraph doc
        Assert.That(graph.Links, Is.Empty)
        Assert.That(graph.Operations, Is.Empty)

[<Test>]
let ``Request body link source and target are parsed correctly`` () =
    match readSpecification "Specifications/links-request-body.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectLinksGraph doc

        let fulfillLink =
            graph.Links |> List.find (fun link -> link.LinkName = "FulfillOrder")

        Assert.That(fulfillLink.Source, Is.EqualTo(RequestBody "orderId"))
        Assert.That(fulfillLink.Target, Is.EqualTo(RequestBodyField "/"))
        Assert.That(fulfillLink.SourceOperationId, Is.EqualTo("createOrder"))
        Assert.That(fulfillLink.TargetOperationId, Is.EqualTo("fulfillOrder"))

[<Test>]
let ``Link without operationId is skipped`` () =
    match readSpecification "Specifications/links-request-body.yaml" with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let graph = collectLinksGraph doc
        Assert.That(graph.Links |> List.exists (fun link -> link.LinkName = "BrokenLink"), Is.False)

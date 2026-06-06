module Microsoft.OpenAPI.FunctionalExtensions.Tests.OperationsTraversal

open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions.OpenApiReaderTools
open OpenApiOperationsTraversal
open ResultEx

[<Test>]
let ``Collect route map from petstore`` () =
    match readSpecification "Specifications/petstore.yaml" with
    | Ok doc ->
        let m = collectRouteMap doc
        Assert.That(m.Routes.Count, Is.GreaterThan(0))
        Assert.That(m.Routes |> Seq.exists (fun r -> r.ParameterSchemas.Length >= 0), Is.True)
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")

[<Test>]
let ``ReturnsArrayViaData is true for data wrapper array response`` () =
    let yaml =
        """
openapi: 3.0.3
info:
  title: Data Wrapper API
  version: 1.0.0
components:
  schemas:
    Widget:
      type: object
      properties:
        id: { type: string }
paths:
  /widgets:
    get:
      operationId: listWidgetsWrapped
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                type: object
                properties:
                  data:
                    type: array
                    items:
                      $ref: '#/components/schemas/Widget'
"""

    match convertToSpecification yaml with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let route =
            collectRouteMap doc
            |> fun map -> map.Routes |> Seq.find (fun r -> r.OperationId = Some "listWidgetsWrapped")

        Assert.That(route.ReturnsArrayViaData, Is.True)
        Assert.That(route.ReturnsArray, Is.False)

[<Test>]
let ``ReturnsArray is true and ReturnsArrayViaData is false for direct array response`` () =
    let yaml =
        """
openapi: 3.0.3
info:
  title: Direct Array API
  version: 1.0.0
components:
  schemas:
    Widget:
      type: object
      properties:
        id: { type: string }
paths:
  /widgets:
    get:
      operationId: listWidgetsDirect
      responses:
        '200':
          description: OK
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/Widget'
"""

    match convertToSpecification yaml with
    | Error e -> Assert.Fail($"Failed to read spec: %A{e}")
    | Ok doc ->
        let route =
            collectRouteMap doc
            |> fun map -> map.Routes |> Seq.find (fun r -> r.OperationId = Some "listWidgetsDirect")

        Assert.That(route.ReturnsArray, Is.True)
        Assert.That(route.ReturnsArrayViaData, Is.False)



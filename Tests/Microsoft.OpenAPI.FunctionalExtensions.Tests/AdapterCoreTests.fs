module Microsoft.OpenAPI.FunctionalExtensions.Tests.AdapterCore

open System
open System.Collections.Generic
open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns

[<Test>]
let ``ofObj returns None for null reference`` () =
    let value: string = null
    Assert.That(AdapterCore.ofObj value, Is.EqualTo(None))

[<Test>]
let ``ofObj returns Some for non-null reference`` () =
    let value = "schema"
    Assert.That(AdapterCore.ofObj value, Is.EqualTo(Some value))

[<Test>]
let ``ofNullable returns None when HasValue is false`` () =
    let value = Nullable<int>()
    Assert.That(AdapterCore.ofNullable value, Is.EqualTo(None))

[<Test>]
let ``ofNullable returns Some when HasValue is true`` () =
    let value = Nullable 42
    Assert.That(AdapterCore.ofNullable value, Is.EqualTo(Some 42))

[<Test>]
let ``readMap returns empty map for null dictionary`` () =
    let dictionary: IDictionary<string, int> = null
    Assert.That(Map.isEmpty (AdapterCore.readMap dictionary), Is.True)

[<Test>]
let ``readMap returns empty map for empty dictionary`` () =
    let dictionary = Dictionary<string, int>()
    Assert.That(Map.isEmpty (AdapterCore.readMap dictionary), Is.True)

[<Test>]
let ``readMap skips null values and keeps populated entries`` () =
    let dictionary = Dictionary<string, string>()
    dictionary.Add("keep", "value")
    dictionary.Add("skip", null)
    let result = AdapterCore.readMap dictionary
    Assert.That(result.Count, Is.EqualTo(1))
    Assert.That(result.["keep"], Is.EqualTo("value"))

[<Test>]
let ``readSeq returns empty list for null sequence`` () =
    let sequence: seq<int> = null
    Assert.That(AdapterCore.readSeq sequence, Is.Empty)

[<Test>]
let ``readSeq returns empty list for empty sequence`` () =
    Assert.That(AdapterCore.readSeq Seq.empty, Is.Empty)

[<Test>]
let ``readSeq skips null elements and keeps populated values`` () =
    let sequence = seq { null; "a"; null; "b" }
    Assert.That(AdapterCore.readSeq sequence = [ "a"; "b" ], Is.True)

[<Test>]
let ``readSet returns empty set for null set`` () =
    let set: ISet<string> = null
    Assert.That(AdapterCore.readSet set, Is.Empty)

[<Test>]
let ``readSet returns empty set for empty set`` () =
    let set = HashSet<string>() :> ISet<string>
    Assert.That(AdapterCore.readSet set, Is.Empty)

[<Test>]
let ``readSet keeps populated values and skips null elements`` () =
    let set = HashSet<string>([ "a"; null; "b" ]) :> ISet<string>
    Assert.That(AdapterCore.readSet set = Set.ofList [ "a"; "b" ], Is.True)

[<Test>]
let ``SchemaRef matches reference schema`` () =
    let schema = OpenApiSchemaReference("Pet", null, null) :> IOpenApiSchema

    match schema with
    | SchemaRef referenceId -> Assert.That(referenceId, Is.EqualTo("Pet"))
    | _ -> Assert.Fail("Expected SchemaRef pattern to match")

[<Test>]
let ``SchemaRef does not match non-reference schema`` () =
    let schema =
        OpenApiSchema(Type = Nullable JsonSchemaType.String) :> IOpenApiSchema

    match schema with
    | SchemaRef _ -> Assert.Fail("SchemaRef should not match a plain schema")
    | _ -> ()

[<Test>]
let ``ArraySchema matches schema with array type and items`` () =
    let itemSchema =
        OpenApiSchema(Type = Nullable JsonSchemaType.String) :> IOpenApiSchema

    let arraySchema =
        OpenApiSchema(
            Type = Nullable JsonSchemaType.Array,
            Items = itemSchema
        )
        :> IOpenApiSchema

    match arraySchema with
    | ArraySchema items -> Assert.That(items, Is.SameAs(itemSchema))
    | _ -> Assert.Fail("Expected ArraySchema pattern to match")

[<Test>]
let ``ObjectSchema matches schema with non-empty properties`` () =
    let properties = Dictionary<string, IOpenApiSchema>()
    properties.Add("name", OpenApiSchema(Type = Nullable JsonSchemaType.String))

    let objectSchema =
        OpenApiSchema(
            Type = Nullable JsonSchemaType.Object,
            Properties = properties
        )
        :> IOpenApiSchema

    match objectSchema with
    | ObjectSchema matchedProperties -> Assert.That(matchedProperties.Count, Is.EqualTo(1))
    | _ -> Assert.Fail("Expected ObjectSchema pattern to match")

[<Test>]
let ``ComposedSchema matches first non-empty composition`` () =
    let first = OpenApiSchema(Type = Nullable JsonSchemaType.String)
    let second = OpenApiSchema(Type = Nullable JsonSchemaType.Integer)
    let oneOf = ResizeArray<IOpenApiSchema>()
    oneOf.Add(first)
    oneOf.Add(second)

    let composedSchema =
        OpenApiSchema(OneOf = oneOf) :> IOpenApiSchema

    match composedSchema with
    | ComposedSchema (CompositionKind.OneOf, schemas) ->
        Assert.That(schemas.Length, Is.EqualTo(2))
        Assert.That(schemas.[0], Is.SameAs(first))
    | _ -> Assert.Fail("Expected ComposedSchema pattern to match")

[<Test>]
let ``NullableType strips Null flag and reports nullability`` () =
    let nullableType = Nullable(JsonSchemaType.String ||| JsonSchemaType.Null)

    match nullableType with
    | NullableType cleanType ->
        Assert.That(cleanType.HasValue, Is.True)
        Assert.That(
            (cleanType.Value &&& JsonSchemaType.Null) = JsonSchemaType.Null,
            Is.False
        )
        Assert.That(
            (cleanType.Value &&& JsonSchemaType.String) = JsonSchemaType.String,
            Is.True
        )
    | NonNullableType _ -> Assert.Fail("Expected NullableType branch")

[<Test>]
let ``NonNullableType preserves schema type without Null flag`` () =
    let schemaType = Nullable JsonSchemaType.Integer

    match schemaType with
    | NullableType _ -> Assert.Fail("Expected NonNullableType branch")
    | NonNullableType cleanType ->
        Assert.That(cleanType.HasValue, Is.True)
        Assert.That(cleanType.Value, Is.EqualTo(JsonSchemaType.Integer))

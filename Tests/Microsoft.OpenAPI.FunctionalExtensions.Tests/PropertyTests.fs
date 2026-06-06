module Microsoft.OpenAPI.FunctionalExtensions.Tests.PropertyTests

open System
open System.Collections.Generic
open System.Text.Json.Nodes
open FsCheck
open FsCheck.FSharp
open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions
open OpenApiSchemaAnalysis

type PropertyGenerators =
    static member NonNullDictionary() : Arbitrary<IDictionary<string, string>> =
        let gen =
            gen {
                let! count = Gen.choose (0, 20)
                let! keys =
                    Gen.arrayOfLength count (
                        ArbMap.generate<NonEmptyString> ArbMap.defaults
                        |> Gen.map (fun (NonEmptyString s) -> s)
                    )

                let! nullFlags = Gen.arrayOfLength count (ArbMap.generate<bool> ArbMap.defaults)

                let dict = Dictionary<string, string>()

                Array.zip keys nullFlags
                |> Array.iter (fun (key, useNull) ->
                    if not (dict.ContainsKey key) then
                        let value = if useNull then null else key
                        dict.Add(key, value))

                return dict :> IDictionary<string, string>
            }

        Arb.fromGen gen

    static member StringListWithNulls() : Arbitrary<string list> =
        let element =
            Gen.oneof [
                Gen.constant null
                ArbMap.generate<NonEmptyString> ArbMap.defaults
                |> Gen.map (fun (NonEmptyString s) -> s)
            ]

        Gen.listOf element |> Arb.fromGen

    static member NullableString() : Arbitrary<string> =
        let gen =
            gen {
                let! isNull = ArbMap.generate<bool> ArbMap.defaults

                if isNull then
                    return null
                else
                    let! (NonEmptyString s) = ArbMap.generate<NonEmptyString> ArbMap.defaults
                    return s
            }

        Arb.fromGen gen

    static member Schema() : Arbitrary<IOpenApiSchema> =
        Gen.elements [
            OpenApiSchema()
            OpenApiSchema(Type = Nullable JsonSchemaType.String)
            OpenApiSchema(Type = Nullable JsonSchemaType.Array, Items = OpenApiSchema())
            OpenApiSchema(
                Properties =
                    Dictionary<string, IOpenApiSchema>(
                        [ KeyValuePair("x", OpenApiSchema() :> IOpenApiSchema) ]
                    )
            )
            OpenApiSchema(Properties = null)
            OpenApiSchema(Items = null)
            OpenApiSchema(AllOf = null)
            OpenApiSchema(OneOf = null)
            OpenApiSchema(AnyOf = null)
            OpenApiSchema(Required = null)
            OpenApiSchema(Enum = null)
            OpenApiSchema(Properties = null, Items = null, AllOf = null, OneOf = null, AnyOf = null)
        ]
        |> Gen.map (fun schema -> schema :> IOpenApiSchema)
        |> Arb.fromGen

[<FsCheck.NUnit.Property(Arbitrary = [| typeof<PropertyGenerators> |])>]
let ``readMap preserves non-null entry count`` (dictionary: IDictionary<string, string>) =
    let expected =
        dictionary
        |> Seq.filter (fun entry -> not (isNull (box entry.Value)))
        |> Seq.length

    let result = AdapterCore.readMap dictionary
    result.Count = expected

[<FsCheck.NUnit.Property(Arbitrary = [| typeof<PropertyGenerators> |])>]
let ``readSeq is idempotent`` (items: string list) =
    let once = AdapterCore.readSeq (Seq.ofList items)
    let twice = AdapterCore.readSeq (Seq.ofList once)
    once = twice

[<FsCheck.NUnit.Property(Arbitrary = [| typeof<PropertyGenerators> |])>]
let ``ofObj returns None for null and Some for non-null`` (value: string) =
    match AdapterCore.ofObj value with
    | None -> isNull value
    | Some actual -> not (isNull value) && actual = value

[<FsCheck.NUnit.Property(Arbitrary = [| typeof<PropertyGenerators> |])>]
let ``SchemaAdapters handle null fields without throwing`` (schema: IOpenApiSchema) =
    SchemaAdapters.schemaTitle schema |> ignore
    SchemaAdapters.schemaDescription schema |> ignore
    SchemaAdapters.schemaType schema |> ignore
    SchemaAdapters.schemaFormat schema |> ignore
    SchemaAdapters.schemaIsNullable schema |> ignore
    SchemaAdapters.schemaProperties schema |> ignore
    SchemaAdapters.schemaRequired schema |> ignore
    SchemaAdapters.schemaItems schema |> ignore
    SchemaAdapters.schemaAllOf schema |> ignore
    SchemaAdapters.schemaOneOf schema |> ignore
    SchemaAdapters.schemaAnyOf schema |> ignore
    SchemaAdapters.schemaNot schema |> ignore
    SchemaAdapters.schemaAdditionalProperties schema |> ignore
    SchemaAdapters.schemaEnum schema |> ignore
    SchemaAdapters.schemaReadOnly schema |> ignore
    SchemaAdapters.schemaWriteOnly schema |> ignore
    SchemaAdapters.schemaDeprecated schema |> ignore

    match schema with
    | :? OpenApiSchema as concrete ->
        if isNull concrete.Properties then
            Assert.That(Map.isEmpty (SchemaAdapters.schemaProperties schema), Is.True)

        if isNull concrete.Items then
            Assert.That(SchemaAdapters.schemaItems schema, Is.EqualTo(None))

        if isNull concrete.AllOf then
            Assert.That(SchemaAdapters.schemaAllOf schema, Is.Empty)

        if isNull concrete.OneOf then
            Assert.That(SchemaAdapters.schemaOneOf schema, Is.Empty)

        if isNull concrete.AnyOf then
            Assert.That(SchemaAdapters.schemaAnyOf schema, Is.Empty)

        if isNull concrete.Required then
            Assert.That(SchemaAdapters.schemaRequired schema, Is.Empty)

        if isNull concrete.Enum then
            Assert.That(SchemaAdapters.schemaEnum schema, Is.Empty)
    | _ -> ()

    true

[<FsCheck.NUnit.Property>]
let ``referencePointer starts with component schema prefix`` (NonEmptyString id) =
    let pointer = ReferenceAdapters.referencePointer id
    pointer.StartsWith("#/components/schemas/")

[<FsCheck.NUnit.Property(Arbitrary = [| typeof<PropertyGenerators> |])>]
let ``classifySchema is total for generated schemas`` (schema: IOpenApiSchema) =
    let _ = classifySchema schema
    true

let private conformingExampleForSchemaType (schemaType: JsonSchemaType) : JsonNode =
    if (schemaType &&& JsonSchemaType.Null) = JsonSchemaType.Null then
        JsonNullSentinel.JsonNull
    elif (schemaType &&& JsonSchemaType.String) = JsonSchemaType.String then
        JsonValue.Create "example" :> JsonNode
    elif (schemaType &&& JsonSchemaType.Integer) = JsonSchemaType.Integer then
        JsonValue.Create 1 :> JsonNode
    elif (schemaType &&& JsonSchemaType.Number) = JsonSchemaType.Number then
        JsonValue.Create 1.5 :> JsonNode
    elif (schemaType &&& JsonSchemaType.Boolean) = JsonSchemaType.Boolean then
        JsonValue.Create true :> JsonNode
    elif (schemaType &&& JsonSchemaType.Array) = JsonSchemaType.Array then
        JsonNode.Parse("[]")
    elif (schemaType &&& JsonSchemaType.Object) = JsonSchemaType.Object then
        JsonNode.Parse("{}")
    else
        JsonValue.Create "fallback" :> JsonNode

let private schemaWithSingleType (schemaType: JsonSchemaType) =
    OpenApiSchema(Type = Nullable schemaType) :> IOpenApiSchema

let private schemaWithNullableType (schemaType: JsonSchemaType) (nullable: bool) =
    let combined =
        if nullable then
            schemaType ||| JsonSchemaType.Null
        else
            schemaType

    OpenApiSchema(Type = Nullable combined) :> IOpenApiSchema

let private singleFlagSchemaTypes =
    [|
        JsonSchemaType.String
        JsonSchemaType.Integer
        JsonSchemaType.Number
        JsonSchemaType.Boolean
        JsonSchemaType.Array
        JsonSchemaType.Object
    |]

[<FsCheck.NUnit.Property>]
let ``conforming examples produce no violations for schema type`` () =
    singleFlagSchemaTypes
    |> Array.forall (fun schemaType ->
        let schema = schemaWithSingleType schemaType
        let example = conformingExampleForSchemaType schemaType

        Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema example "property-test"
        |> List.isEmpty)

[<FsCheck.NUnit.Property>]
let ``null example violates iff schema is not nullable`` (nullable: bool) =
    let schema = schemaWithNullableType JsonSchemaType.String nullable
    let violations =
        Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample
            schema
            JsonNullSentinel.JsonNull
            "property-test"

    if nullable then
        List.isEmpty violations
    else
        not (List.isEmpty violations)

[<FsCheck.NUnit.Property>]
let ``string examples always pass string schemas`` (value: NonEmptyString) =
    let schema = schemaWithSingleType JsonSchemaType.String
    let example = JsonValue.Create value.Get :> JsonNode
    Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema example "property-test"
    |> List.isEmpty

[<FsCheck.NUnit.Property>]
let ``integer examples always fail against string-only schemas`` () =
    let schema = schemaWithSingleType JsonSchemaType.String
    let example = JsonValue.Create 42 :> JsonNode
    Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample schema example "property-test"
    |> List.isEmpty |> not

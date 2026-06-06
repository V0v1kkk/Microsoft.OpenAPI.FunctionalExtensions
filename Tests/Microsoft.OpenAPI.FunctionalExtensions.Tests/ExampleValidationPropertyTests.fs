module Microsoft.OpenAPI.FunctionalExtensions.Tests.ExampleValidationPropertyTests

open System
open System.Collections.Generic
open System.Text.Json
open System.Text.Json.Nodes
open FsCheck
open FsCheck.FSharp
open NUnit.Framework
open Microsoft.OpenApi
open Microsoft.OpenAPI.FunctionalExtensions
open Microsoft.OpenAPI.FunctionalExtensions.ActivePatterns
open Microsoft.OpenAPI.FunctionalExtensions.Linting.Types

let private validate = Microsoft.OpenAPI.FunctionalExtensions.Linting.ExampleValidation.validateExample

let private genBool = ArbMap.generate<bool> ArbMap.defaults

let private genNonEmptyString =
    ArbMap.generate<NonEmptyString> ArbMap.defaults
    |> Gen.map (fun (NonEmptyString text) -> text)

let private sequenceGens (gens: Gen<'a> list) : Gen<'a list> =
    let folder (head: Gen<'a>) (tail: Gen<'a list>) =
        gen {
            let! value = head
            let! values = tail
            return value :: values
        }

    List.foldBack folder gens (Gen.constant [])

let private hasViolations = not << List.isEmpty

let private satisfiesAllBranches (branches: IOpenApiSchema list) (example: JsonNode) =
    branches
    |> List.forall (fun branch -> validate branch example "generator" |> List.isEmpty)

let private maxSchemaDepth = 4

let private primitiveTypes =
    [|
        JsonSchemaType.String
        JsonSchemaType.Integer
        JsonSchemaType.Number
        JsonSchemaType.Boolean
    |]

let private propertyName index = $"prop{index}"

let private cloneNode (node: JsonNode) =
    if isNull node then
        null
    else
        node.DeepClone()

let private toJsonArray (items: JsonNode list) =
    let array = JsonArray()

    for item in items do
        array.Add(cloneNode item)

    array :> JsonNode

let private addObjectProperty (jsonObject: JsonObject) (propertyName: string) (value: JsonNode) =
    jsonObject.Add(propertyName, cloneNode value)

let private buildPrimitiveSchema (schemaType: JsonSchemaType) (nullable: bool) =
    let combined =
        if nullable then
            schemaType ||| JsonSchemaType.Null
        else
            schemaType

    let schema = OpenApiSchema()
    schema.Type <- Nullable combined
    schema :> IOpenApiSchema

let private buildEnumSchema (values: string list) =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.String

    schema.Enum <-
        List<JsonNode>(values |> List.map (fun value -> JsonValue.Create value :> JsonNode))
        :> IList<JsonNode>

    schema :> IOpenApiSchema

let private buildDateFormatSchema () =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.String
    schema.Format <- "date"
    schema :> IOpenApiSchema

let private buildObjectSchema
    (properties: (string * IOpenApiSchema) list)
    (required: string list)
    (additionalPropertiesAllowed: bool)
    =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.Object
    schema.Properties <- Dictionary<string, IOpenApiSchema>() :> IDictionary<string, IOpenApiSchema>

    for propertyName, propertySchema in properties do
        schema.Properties.Add(propertyName, propertySchema) |> ignore

    schema.Required <- HashSet<string>(required) :> ISet<string>
    schema.AdditionalPropertiesAllowed <- additionalPropertiesAllowed
    schema :> IOpenApiSchema

let private buildArraySchema (items: IOpenApiSchema) =
    let schema = OpenApiSchema()
    schema.Type <- Nullable JsonSchemaType.Array
    schema.Items <- items
    schema :> IOpenApiSchema

let private buildCompositionSchema (kind: CompositionKind) (branches: IOpenApiSchema list) =
    let schema = OpenApiSchema()
    let list = List<IOpenApiSchema>(branches)

    match kind with
    | AllOf -> schema.AllOf <- list
    | OneOf -> schema.OneOf <- list
    | AnyOf -> schema.AnyOf <- list

    schema :> IOpenApiSchema

let private primitiveExample (schemaType: JsonSchemaType) : JsonNode =
    if (schemaType &&& JsonSchemaType.Null) = JsonSchemaType.Null then
        JsonNullSentinel.JsonNull
    elif (schemaType &&& JsonSchemaType.String) = JsonSchemaType.String then
        JsonValue.Create "example" :> JsonNode
    elif (schemaType &&& JsonSchemaType.Integer) = JsonSchemaType.Integer then
        JsonValue.Create 42 :> JsonNode
    elif (schemaType &&& JsonSchemaType.Number) = JsonSchemaType.Number then
        JsonValue.Create 3.14 :> JsonNode
    elif (schemaType &&& JsonSchemaType.Boolean) = JsonSchemaType.Boolean then
        JsonValue.Create true :> JsonNode
    else
        JsonValue.Create "fallback" :> JsonNode

let private wrongKindExample (schemaTypes: JsonSchemaType list) =
    let hasString = schemaTypes |> List.exists ((=) JsonSchemaType.String)
    let hasInteger = schemaTypes |> List.exists ((=) JsonSchemaType.Integer)
    let hasNumber = schemaTypes |> List.exists ((=) JsonSchemaType.Number)
    let hasBoolean = schemaTypes |> List.exists ((=) JsonSchemaType.Boolean)
    let hasArray = schemaTypes |> List.exists ((=) JsonSchemaType.Array)
    let hasObject = schemaTypes |> List.exists ((=) JsonSchemaType.Object)

    if hasString && hasInteger && hasNumber && hasBoolean then
        JsonNode.Parse("[]")
    elif hasString || hasInteger || hasNumber || hasBoolean then
        JsonNode.Parse("{}")
    elif hasArray then
        JsonValue.Create "not-an-array" :> JsonNode
    elif hasObject then
        JsonValue.Create 999 :> JsonNode
    else
        JsonValue.Create "mismatch" :> JsonNode

let rec private genConformingExample (schema: IOpenApiSchema) (depth: int) : Gen<JsonNode> =
    if depth <= 0 then
        Gen.constant (JsonValue.Create "depth-limit" :> JsonNode)
    else
        match schema with
        | ComposedSchema(kind, branches) ->
            match kind with
            | AllOf ->
                gen {
                    if List.isEmpty branches then
                        return JsonValue.Create "empty-allof" :> JsonNode
                    else
                        let! candidates =
                            branches
                            |> List.map (fun branch -> genConformingExample branch (depth - 1))
                            |> sequenceGens

                        let satisfying =
                            candidates
                            |> List.tryFind (satisfiesAllBranches branches)

                        match satisfying with
                        | Some example -> return example
                        | None ->
                            let allObjects =
                                candidates |> List.forall (fun (example: JsonNode) -> example :? JsonObject)

                            if allObjects then
                                let merged = JsonObject()

                                for example in candidates do
                                    let source = example :?> JsonObject

                                    for property in source do
                                        merged[property.Key] <- property.Value.DeepClone()

                                let mergedNode = merged :> JsonNode

                                if satisfiesAllBranches branches mergedNode then
                                    return mergedNode
                                else
                                    return! genConformingExample (List.head branches) (depth - 1)
                            else
                                return! genConformingExample (List.head branches) (depth - 1)
                }
            | OneOf
            | AnyOf ->
                Gen.oneof (branches |> List.map (fun branch -> genConformingExample branch (depth - 1)))
        | ArraySchema items ->
            gen {
                let! itemCount = Gen.choose (0, 3)
                let! items = Gen.listOfLength itemCount (genConformingExample items (depth - 1))
                return toJsonArray items
            }
        | ObjectSchema _ ->
            let properties = SchemaAdapters.schemaProperties schema
            let required = SchemaAdapters.schemaRequired schema

            properties
            |> Map.toList
            |> List.map (fun (propertyName, propertySchema) ->
                gen {
                    let isRequired = Set.contains propertyName required
                    let! includeProperty = if isRequired then Gen.constant true else genBool

                    if includeProperty then
                        let! value = genConformingExample propertySchema (depth - 1)
                        return Some(propertyName, value)
                    else
                        return None
                })
            |> sequenceGens
            |> Gen.map (fun entries ->
                let jsonObject = JsonObject()

                for entry in entries do
                    match entry with
                    | Some(propertyName, value) -> addObjectProperty jsonObject propertyName value
                    | None -> ()

                jsonObject :> JsonNode)
        | _ ->
            match SchemaAdapters.schemaFormat schema with
            | Some "date" -> Gen.constant (JsonValue.Create "2024-06-15" :> JsonNode)
            | _ ->
                match SchemaAdapters.schemaEnum schema with
                | enumValue :: _ -> Gen.constant (cloneNode enumValue)
                | [] ->
                    match SchemaAdapters.schemaType schema with
                    | Some schemaType -> Gen.constant (primitiveExample schemaType)
                    | None -> Gen.constant (JsonValue.Create "fallback" :> JsonNode)

and private genNonConformingExample (schema: IOpenApiSchema) (depth: int) : Gen<JsonNode> =
    if depth <= 0 then
        Gen.constant (JsonValue.Create 0 :> JsonNode)
    else
        match schema with
        | ComposedSchema(kind, branches) ->
            match kind with
            | AllOf ->
                gen {
                    let! branch = Gen.elements branches
                    return! genNonConformingExample branch (depth - 1)
                }
            | OneOf
            | AnyOf ->
                let schemaTypes =
                    branches
                    |> List.collect (fun branch ->
                        match SchemaAdapters.schemaType branch with
                        | Some schemaType ->
                            [
                                if (schemaType &&& JsonSchemaType.String) = JsonSchemaType.String then
                                    JsonSchemaType.String
                                if (schemaType &&& JsonSchemaType.Integer) = JsonSchemaType.Integer then
                                    JsonSchemaType.Integer
                                if (schemaType &&& JsonSchemaType.Number) = JsonSchemaType.Number then
                                    JsonSchemaType.Number
                                if (schemaType &&& JsonSchemaType.Boolean) = JsonSchemaType.Boolean then
                                    JsonSchemaType.Boolean
                                if (schemaType &&& JsonSchemaType.Array) = JsonSchemaType.Array then
                                    JsonSchemaType.Array
                                if (schemaType &&& JsonSchemaType.Object) = JsonSchemaType.Object then
                                    JsonSchemaType.Object
                            ]
                        | None ->
                            match branch with
                            | ObjectSchema _ -> [ JsonSchemaType.Object ]
                            | ArraySchema _ -> [ JsonSchemaType.Array ]
                            | _ -> [])
                    |> List.distinct

                Gen.constant (wrongKindExample schemaTypes)
        | ArraySchema items ->
            gen {
                let! badItem = genNonConformingExample items (depth - 1)
                return toJsonArray [ badItem ]
            }
        | ObjectSchema _ ->
            let properties = SchemaAdapters.schemaProperties schema
            let required = SchemaAdapters.schemaRequired schema

            Gen.oneof [
                if not (Set.isEmpty required) then
                    gen {
                        let! missing =
                            required |> Set.toList |> Gen.elements

                        let jsonObject = JsonObject()

                        for propertyName, propertySchema in Map.toList properties do
                            if propertyName <> missing then
                                let! value = genConformingExample propertySchema (depth - 1)
                                addObjectProperty jsonObject propertyName value

                        return jsonObject :> JsonNode
                    }
                if not schema.AdditionalPropertiesAllowed && not (Map.isEmpty properties) then
                    gen {
                        let! extraName = Gen.elements [ "extraProp"; "_unknown"; "surprise" ]
                        let jsonObject = JsonObject()

                        for propertyName, propertySchema in Map.toList properties do
                            let! value = genConformingExample propertySchema (depth - 1)
                            addObjectProperty jsonObject propertyName value

                        jsonObject.Add(extraName, JsonValue.Create true)
                        return jsonObject :> JsonNode
                    }
                gen {
                    let! propertyName, propertySchema =
                        properties |> Map.toList |> Gen.elements

                    let jsonObject = JsonObject()

                    for name, childSchema in Map.toList properties do
                        if name = propertyName then
                            let! badValue = genNonConformingExample childSchema (depth - 1)
                            addObjectProperty jsonObject name badValue
                        else
                            let! value = genConformingExample childSchema (depth - 1)
                            addObjectProperty jsonObject name value

                    return jsonObject :> JsonNode
                }
            ]
        | _ ->
            match SchemaAdapters.schemaEnum schema with
            | _ :: _ as enumValues ->
                let allowed =
                    enumValues
                    |> List.choose (fun enumValue ->
                        match enumValue with
                        | :? JsonValue as value when value.GetValueKind() = JsonValueKind.String ->
                            Some(value.GetValue<string>())
                        | _ -> None)

                gen {
                    let! candidate = genNonEmptyString

                    if List.contains candidate allowed then
                        return JsonValue.Create $"{candidate}_invalid" :> JsonNode
                    else
                        return JsonValue.Create candidate :> JsonNode
                }
            | [] ->
                let schemaTypes =
                    match SchemaAdapters.schemaType schema with
                    | Some schemaType ->
                        [
                            if (schemaType &&& JsonSchemaType.String) = JsonSchemaType.String then
                                JsonSchemaType.String
                            if (schemaType &&& JsonSchemaType.Integer) = JsonSchemaType.Integer then
                                JsonSchemaType.Integer
                            if (schemaType &&& JsonSchemaType.Number) = JsonSchemaType.Number then
                                JsonSchemaType.Number
                            if (schemaType &&& JsonSchemaType.Boolean) = JsonSchemaType.Boolean then
                                JsonSchemaType.Boolean
                        ]
                    | None -> []

                if List.isEmpty schemaTypes then
                    Gen.constant (JsonValue.Create 0 :> JsonNode)
                else
                    Gen.constant (wrongKindExample schemaTypes)

and private fallbackExample (schema: IOpenApiSchema) : JsonNode =
    match schema with
    | ArraySchema _ -> JsonNode.Parse("[]")
    | ObjectSchema _ -> JsonNode.Parse("{}")
    | ComposedSchema(AllOf, branches) ->
        branches
        |> List.tryPick (fun branch ->
            let candidate = fallbackExample branch

            if validate branch candidate "generator" |> List.isEmpty then
                Some candidate
            else
                None)
        |> Option.defaultValue (JsonValue.Create "fallback" :> JsonNode)
    | ComposedSchema(OneOf, branches)
    | ComposedSchema(AnyOf, branches) ->
        match branches with
        | branch :: _ -> fallbackExample branch
        | [] -> JsonValue.Create "fallback" :> JsonNode
    | _ ->
        match SchemaAdapters.schemaFormat schema with
        | Some "date" -> JsonValue.Create "2024-06-15" :> JsonNode
        | _ ->
            match SchemaAdapters.schemaType schema with
            | Some schemaType -> primitiveExample schemaType
            | None -> JsonValue.Create "fallback" :> JsonNode

and private pickValidatedExample
    (schema: IOpenApiSchema)
    (depth: int)
    (attemptCount: int)
    (generate: int -> Gen<JsonNode>)
    (isValid: JsonNode -> bool)
    =
    gen {
        let! candidates = Gen.listOfLength attemptCount (generate depth)
        let fallback = fallbackExample schema

        return
            candidates
            |> List.tryFind isValid
            |> Option.defaultValue fallback
    }

and private genLeafSchema (depth: int) : Gen<IOpenApiSchema> =
    Gen.oneof [
        gen {
            let! nullable = genBool
            let! schemaType = Gen.elements primitiveTypes
            return buildPrimitiveSchema schemaType nullable
        }
        gen {
            let! valueCount = Gen.choose (2, 5)
            let! values = Gen.arrayOfLength valueCount genNonEmptyString

            return buildEnumSchema (values |> Array.toList)
        }
        Gen.constant (buildDateFormatSchema ())
    ]

and private genObjectSchema (depth: int) : Gen<IOpenApiSchema> =
    gen {
        let! propertyCount = Gen.choose (0, 5)
        let! additionalAllowed = genBool

        let! requiredCount =
            if propertyCount = 0 then
                Gen.constant 0
            else
                Gen.choose (0, propertyCount)

        let! childSchemas =
            Gen.listOfLength propertyCount (genSchema (depth - 1))

        let properties =
            childSchemas
            |> List.mapi (fun index childSchema -> propertyName index, childSchema)

        let required =
            properties
            |> List.take requiredCount
            |> List.map fst

        return buildObjectSchema properties required additionalAllowed
    }

and private genArraySchema (depth: int) : Gen<IOpenApiSchema> =
    gen {
        let! items = genSchema (depth - 1)
        return buildArraySchema items
    }

and private genCompositionSchema (depth: int) : Gen<IOpenApiSchema> =
    gen {
        let! kind = Gen.elements [ AllOf; OneOf; AnyOf ]
        let! branchCount = Gen.choose (2, 3)

        let! branches =
            match kind with
            | AllOf ->
                gen {
                    let! nullable = genBool
                    let! schemaType = Gen.elements primitiveTypes
                    let shared = buildPrimitiveSchema schemaType nullable

                    return List.init branchCount (fun _ -> shared)
                }
            | OneOf
            | AnyOf -> Gen.listOfLength branchCount (genLeafSchema (depth - 1))

        return buildCompositionSchema kind branches
    }

and private genSchema (depth: int) : Gen<IOpenApiSchema> =
    if depth <= 0 then
        genLeafSchema depth
    else
        Gen.oneof [
            genLeafSchema depth
            genObjectSchema depth
            genArraySchema depth
            genCompositionSchema depth
        ]

let private genSchemaSized : Gen<IOpenApiSchema> =
    Gen.sized (fun size -> genSchema (min maxSchemaDepth (size / 4 + 1)))

let private genConformingPair : Gen<IOpenApiSchema * JsonNode> =
    gen {
        let! schema = genSchemaSized

        let! example =
            pickValidatedExample
                schema
                maxSchemaDepth
                16
                (fun depth -> genConformingExample schema depth)
                (fun candidate -> validate schema candidate "property-test" |> List.isEmpty)

        return schema, example
    }
    |> Gen.filter (fun (schema, example) -> validate schema example "property-test" |> List.isEmpty)

let private genSchemaWithNonConformingExample : Gen<IOpenApiSchema * JsonNode> =
    gen {
        let! schema = genSchemaSized

        let! example =
            pickValidatedExample
                schema
                maxSchemaDepth
                16
                (fun depth -> genNonConformingExample schema depth)
                (fun candidate -> validate schema candidate "property-test" |> hasViolations)

        return schema, example
    }
    |> Gen.filter (fun (schema, example) -> validate schema example "property-test" |> hasViolations)

let private noShrinkArb (gen: Gen<'a>) =
    Arb.fromGenShrink (gen, fun _ -> Seq.empty)

let private genJsonNode : Gen<JsonNode> =
    Gen.oneof [
        Gen.constant JsonNullSentinel.JsonNull
        genNonEmptyString |> Gen.map (fun text -> JsonValue.Create text :> JsonNode)
        Gen.choose (-100, 100) |> Gen.map (fun value -> JsonValue.Create value :> JsonNode)
        Gen.choose (1, 100) |> Gen.map (fun value -> JsonValue.Create (float value) :> JsonNode)
        genBool |> Gen.map (fun value -> JsonValue.Create value :> JsonNode)
        Gen.constant (JsonNode.Parse("[]"))
        Gen.constant (JsonNode.Parse("{}"))
    ]

type ExampleValidationPropertyGenerators =
    static member Schema() = genSchemaSized |> Arb.fromGen

    static member JsonNode() = genJsonNode |> Arb.fromGen

let private hasErrorViolations violations =
    violations |> List.exists (fun violation -> violation.Severity = Error)

let private hasInfoViolations violations =
    violations |> List.exists (fun violation -> violation.Severity = Info)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``conforming generated examples produce no violations`` () =
    Prop.forAll (noShrinkArb genConformingPair) (fun (schema, example) ->
        validate schema example "property-test" |> List.isEmpty)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``non-conforming generated examples produce at least one violation`` () =
    Prop.forAll (noShrinkArb genSchemaWithNonConformingExample) (fun (schema, example) ->
        validate schema example "property-test" |> hasViolations)

[<FsCheck.NUnit.Property(MaxTest = 500, Arbitrary = [| typeof<ExampleValidationPropertyGenerators> |])>]
let ``validateExample never throws for random schema and example`` (schema: IOpenApiSchema, example: JsonNode) =
    let _ = validate schema example "property-test"
    true

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``empty arrays always validate regardless of item schema`` () =
    Prop.forAll (Arb.fromGen genSchemaSized) (fun itemSchema ->
        let schema = buildArraySchema itemSchema
        let empty = JsonNode.Parse("[]")
        validate schema empty "property-test" |> List.isEmpty)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``allOf with same primitive branches accepts conforming primitive`` () =
    Prop.forAll
        (Arb.fromGen (
            gen {
                let! nullable = genBool
                let! schemaType = Gen.elements primitiveTypes
                let branch = buildPrimitiveSchema schemaType nullable
                let schema = buildCompositionSchema AllOf [ branch; branch ]
                let example = primitiveExample schemaType
                return schema, example
            }
        ))
        (fun (schema, example) -> validate schema example "property-test" |> List.isEmpty)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``allOf with conflicting primitive branches always reports violation`` () =
    let schema =
        buildCompositionSchema
            AllOf
            [
                buildPrimitiveSchema JsonSchemaType.String false
                buildPrimitiveSchema JsonSchemaType.Integer false
            ]

    Prop.forAll (Arb.fromGen genJsonNode) (fun example ->
        let violations = validate schema example "property-test"
        hasViolations violations)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``oneOf with distinct primitive branches rejects values matching none`` () =
    let schema =
        buildCompositionSchema
            OneOf
            [
                buildPrimitiveSchema JsonSchemaType.String false
                buildPrimitiveSchema JsonSchemaType.Integer false
            ]

    let example = JsonValue.Create true :> JsonNode
    hasViolations (validate schema example "property-test")

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``oneOf with distinct primitive branches accepts matching branch`` () =
    let schema =
        buildCompositionSchema
            OneOf
            [
                buildPrimitiveSchema JsonSchemaType.String false
                buildPrimitiveSchema JsonSchemaType.Integer false
            ]

    validate schema (JsonValue.Create "hello" :> JsonNode) "property-test" |> List.isEmpty

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``anyOf with distinct primitive branches accepts matching branch`` () =
    let schema =
        buildCompositionSchema
            AnyOf
            [
                buildPrimitiveSchema JsonSchemaType.String false
                buildPrimitiveSchema JsonSchemaType.Boolean false
            ]

    validate schema (JsonValue.Create false :> JsonNode) "property-test" |> List.isEmpty

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``anyOf with distinct primitive branches rejects values matching none`` () =
    let schema =
        buildCompositionSchema
            AnyOf
            [
                buildPrimitiveSchema JsonSchemaType.String false
                buildPrimitiveSchema JsonSchemaType.Integer false
            ]

    let example = JsonNode.Parse("[]")
    hasViolations (validate schema example "property-test")

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``nested allOf containing oneOf accepts conforming example`` () =
    let schema =
        buildCompositionSchema
            AllOf
            [
                buildCompositionSchema
                    OneOf
                    [
                        buildPrimitiveSchema JsonSchemaType.String false
                        buildPrimitiveSchema JsonSchemaType.Integer false
                    ]
                buildPrimitiveSchema JsonSchemaType.String false
            ]

    validate schema (JsonValue.Create "nested" :> JsonNode) "property-test" |> List.isEmpty

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``enum conforming values produce no violations`` () =
    Prop.forAll
        (Arb.fromGen (
            gen {
                let! valueCount = Gen.choose (2, 5)
                let! values =
                    Gen.arrayOfLength valueCount (
                        genNonEmptyString
                    )

                let enumValues = values |> Array.toList
                let schema = buildEnumSchema enumValues
                let! index = Gen.choose (0, enumValues.Length - 1)
                return schema, JsonValue.Create enumValues[index] :> JsonNode
            }
        ))
        (fun (schema, example) -> validate schema example "property-test" |> List.isEmpty)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``enum non-conforming values produce violations`` () =
    Prop.forAll
        (Arb.fromGen (
            gen {
                let! valueCount = Gen.choose (2, 5)
                let! values =
                    Gen.arrayOfLength valueCount (
                        genNonEmptyString
                    )

                let enumValues = values |> Array.toList |> Set.ofList
                let schema = buildEnumSchema (enumValues |> Set.toList)

                let! candidate =
                    genNonEmptyString |> Gen.filter (fun text -> not (Set.contains text enumValues))

                return schema, JsonValue.Create candidate :> JsonNode
            }
        ))
        (fun (schema, example) -> validate schema example "property-test" |> hasViolations)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``valid date format examples produce no info violations`` () =
    Prop.forAll (Gen.choose (2000, 2030) |> Arb.fromGen) (fun year ->
        let schema = buildDateFormatSchema ()
        let example = JsonValue.Create (sprintf "%04d-06-15" year) :> JsonNode
        validate schema example "property-test" |> hasInfoViolations |> not)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``invalid date format examples produce info violations`` () =
    let schema = buildDateFormatSchema ()

    Prop.forAll
        (genNonEmptyString
         |> Gen.filter (fun text -> DateOnly.TryParse(text) |> fst |> not)
         |> Arb.fromGen)
        (fun text ->
            let example = JsonValue.Create text :> JsonNode
            validate schema example "property-test" |> hasInfoViolations)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``object with all required properties conforming produces no violations`` () =
    Prop.forAll
        (Arb.fromGen (
            gen {
                let! propertyCount = Gen.choose (1, 5)
                let! additionalAllowed = genBool

                let! childSchemas =
                    Gen.listOfLength propertyCount (genLeafSchema maxSchemaDepth)

                let properties =
                    childSchemas
                    |> List.mapi (fun index childSchema -> propertyName index, childSchema)

                let required = properties |> List.map fst
                let schema = buildObjectSchema properties required additionalAllowed

                let! example =
                    properties
                    |> List.map (fun (propertyName, propertySchema) ->
                        gen {
                            let! value = genConformingExample propertySchema maxSchemaDepth
                            return propertyName, value
                        })
                    |> sequenceGens
                    |> Gen.map (fun entries ->
                        let jsonObject = JsonObject()

                        for propertyName, value in entries do
                            addObjectProperty jsonObject propertyName value

                        jsonObject :> JsonNode)

                return schema, example
            }
        ))
        (fun (schema, example) -> validate schema example "property-test" |> List.isEmpty)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``object missing a required property produces violation`` () =
    Prop.forAll
        (Arb.fromGen (
            gen {
                let! propertyCount = Gen.choose (2, 5)

                let! childSchemas =
                    Gen.listOfLength propertyCount (genLeafSchema maxSchemaDepth)

                let properties =
                    childSchemas
                    |> List.mapi (fun index childSchema -> propertyName index, childSchema)

                let required = properties |> List.map fst
                let schema = buildObjectSchema properties required false

                let! missingIndex = Gen.choose (0, propertyCount - 1)

                let! example =
                    properties
                    |> List.indexed
                    |> List.choose (fun (index, (propertyName, propertySchema)) ->
                        if index = missingIndex then
                            None
                        else
                            Some(
                                gen {
                                    let! value = genConformingExample propertySchema maxSchemaDepth
                                    return propertyName, value
                                }
                            ))
                    |> sequenceGens
                    |> Gen.map (fun entries ->
                        let jsonObject = JsonObject()

                        for propertyName, value in entries do
                            addObjectProperty jsonObject propertyName value

                        jsonObject :> JsonNode)

                return schema, example
            }
        ))
        (fun (schema, example) -> validate schema example "property-test" |> hasViolations)

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``object with extra property and additionalProperties false produces violation`` () =
    let inner = buildPrimitiveSchema JsonSchemaType.String false
    let schema = buildObjectSchema [ "value", inner ] [ "value" ] false
    let example = JsonNode.Parse("""{"value": "ok", "extra": 1}""")
    hasViolations (validate schema example "property-test")

[<FsCheck.NUnit.Property(MaxTest = 500)>]
let ``nested object inner property violations are reported`` () =
    Prop.forAll
        (Arb.fromGen (
            gen {
                let inner =
                    buildObjectSchema
                        [ "count", buildPrimitiveSchema JsonSchemaType.Integer false ]
                        [ "count" ]
                        true

                let outer = buildObjectSchema [ "inner", inner ] [ "inner" ] true
                return outer
            }
        ))
        (fun schema ->
            let valid = JsonNode.Parse("""{"inner": {"count": 3}}""")
            let invalid = JsonNode.Parse("""{"inner": {}}""")

            validate schema valid "property-test" |> List.isEmpty
            && validate schema invalid "property-test" |> hasViolations)

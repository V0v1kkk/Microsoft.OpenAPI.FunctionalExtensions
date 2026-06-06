[<NUnit.Framework.NonParallelizable>]
module Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tests.Tool

open System
open System.IO
open System.Text
open System.Text.Json
open NUnit.Framework

let private repoRoot =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

let private findToolDllInConfig (basePath: string) (configuration: string) =
    let configPath = Path.Combine(basePath, configuration)
    if not (Directory.Exists configPath) then None
    else
        Directory.EnumerateDirectories(configPath, "net*")
        |> Seq.sortByDescending (fun tfmDir ->
            let tfm = Path.GetFileName tfmDir
            if tfm.StartsWith("net", StringComparison.Ordinal) then
                match Int32.TryParse(tfm.Substring(3).Split('.').[0]) with
                | true, version -> version
                | _ -> 0
            else
                0)
        |> Seq.tryPick (fun tfmDir ->
            let path = Path.Combine(tfmDir, "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool.dll")
            if File.Exists path then Some path else None)

let private resolveToolDll () =
    let basePath = Path.Combine(repoRoot, "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool", "bin")
    [ "Debug"; "Release" ]
    |> List.tryPick (findToolDllInConfig basePath)
    |> Option.defaultWith (fun () ->
        failwith "Visualizing tool DLL not found. Build the solution before running these tests.")

let private isGraphvizAvailable () =
    try
        let psi = Diagnostics.ProcessStartInfo("dot", "-V")
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        psi.CreateNoWindow <- true
        use p = Diagnostics.Process.Start(psi)
        if not (p.WaitForExit(5000)) then
            try p.Kill(entireProcessTree = true) with _ -> ()
            false
        else
            p.ExitCode = 0
    with _ ->
        false

let private runTool args =
    let timeoutMs = 30000
    let toolDll = resolveToolDll ()
    let psi =
        Diagnostics.ProcessStartInfo(
            FileName = "dotnet",
            Arguments = $"exec \"{toolDll}\" {args}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        )

    use p = Diagnostics.Process.Start(psi)
    let stdout = StringBuilder()
    let stderr = StringBuilder()

    p.OutputDataReceived.Add(fun e ->
        if not (isNull e.Data) then stdout.AppendLine(e.Data) |> ignore)

    p.ErrorDataReceived.Add(fun e ->
        if not (isNull e.Data) then stderr.AppendLine(e.Data) |> ignore)

    p.BeginOutputReadLine()
    p.BeginErrorReadLine()

    let exited = p.WaitForExit(timeoutMs)
    if not exited then
        try p.Kill(entireProcessTree = true) with _ -> ()
        p.WaitForExit(5000) |> ignore
        124, stdout.ToString(), stderr.ToString() + Environment.NewLine + "Process timed out"
    else
        p.WaitForExit()
        p.ExitCode, stdout.ToString(), stderr.ToString()

let private linksSpecPath =
    Path.Combine(repoRoot, "Tests", "Microsoft.OpenAPI.FunctionalExtensions.Tests", "Specifications", "links-example.yaml")

let private outDir =
    Path.Combine(repoRoot, "out")

let private ensureOutDir () =
    Directory.CreateDirectory(outDir) |> ignore

let private writeTempSpec (fileName: string) (content: string) =
    ensureOutDir ()
    let path = Path.Combine(outDir, fileName) |> Path.GetFullPath
    File.WriteAllText(path, content)
    path

let private parseJsonRoot (json: string) =
    use doc = JsonDocument.Parse(json)
    doc.RootElement.Clone()

let private hasJsonProperty (element: JsonElement) (name: string) =
    let found, _ = element.TryGetProperty(name)
    found

[<SetUp>]
let requireGraphvizForSvgTests () =
    let testName = TestContext.CurrentContext.Test.Name
    if testName.Contains("dot") || testName.Contains("collect") || testName.Contains("merge") || testName.Contains("scissors") || testName.Contains("version") || testName.Contains("json") || testName.Contains("exit code") then
        ()
    else
        Assume.That(isGraphvizAvailable (), Is.True, "Graphviz (dot) is not available on PATH")

[<Test>]
let ``lint petstore reports violations`` () =
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code, stdout, err = runTool ($"--lint --input {inputSpec}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), "Lint should exit 0 when only warnings/info, or 1 for errors")
    Assert.That(stdout.Contains("empty-schema-property-description"), Is.True, "Expected lint output to mention a rule id")

[<Test>]
let ``lint with disabled rule omits that rule from output`` () =
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code, stdout, err =
        runTool ($"--lint --input {inputSpec} --disable-rule empty-schema-property-description")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0).Or.EqualTo(1), err)
    Assert.That(stdout.Contains("empty-schema-property-description"), Is.False)

[<Test>]
let ``schema svg from petstore`` () =
    let outDir = Path.Combine(repoRoot, "out")
    Directory.CreateDirectory(outDir) |> ignore
    let outFile = Path.Combine(outDir, "schema.svg") |> Path.GetFullPath
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code, stdout, err = runTool ($"--schema-svg --input {inputSpec} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)

[<Test>]
let ``route svg from petstore`` () =
    let outDir = Path.Combine(repoRoot, "out")
    Directory.CreateDirectory(outDir) |> ignore
    let outFile = Path.Combine(outDir, "routes.svg") |> Path.GetFullPath
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code, stdout, err = runTool ($"--route-svg --input {inputSpec} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)

[<Test>]
let ``schema dot contains array element type`` () =
    let outDir = Path.Combine(repoRoot, "out")
    Directory.CreateDirectory(outDir) |> ignore
    let outFile = Path.Combine(outDir, "schema.dot") |> Path.GetFullPath
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code, stdout, err = runTool ($"--schema-svg --dot --input {inputSpec} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    let dot = File.ReadAllText(outFile)
    Assert.That(dot.Contains("array[Pet]"), Is.True, "DOT should include array[Pet] label")

[<Test>]
let ``links svg from links example`` () =
    ensureOutDir ()
    let outFile = Path.Combine(outDir, "links.svg") |> Path.GetFullPath
    let code, stdout, err = runTool ($"--links-svg --input {linksSpecPath} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)
    let svg = File.ReadAllText(outFile)
    Assert.That(svg.Length, Is.GreaterThan(0))
    Assert.That(svg.Contains("<svg"), Is.True)

[<Test>]
let ``links collect produces valid json with expected link count`` () =
    ensureOutDir ()
    let outFile = Path.Combine(outDir, "links.json") |> Path.GetFullPath
    let code, stdout, err = runTool ($"--links-collect --input {linksSpecPath} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)
    let root = parseJsonRoot (File.ReadAllText(outFile))
    Assert.That(hasJsonProperty root "links", Is.True)
    Assert.That(root.GetProperty("links").GetArrayLength(), Is.EqualTo(3))

[<Test>]
let ``schema collect produces valid json`` () =
    ensureOutDir ()
    let outFile = Path.Combine(outDir, "schema.json") |> Path.GetFullPath
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code, stdout, err = runTool ($"--schema-collect --input {inputSpec} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)
    let root = parseJsonRoot (File.ReadAllText(outFile))
    Assert.That(hasJsonProperty root "nodes", Is.True)
    Assert.That(hasJsonProperty root "edges", Is.True)
    Assert.That(root.GetProperty("nodes").GetArrayLength(), Is.GreaterThan(0))

[<Test>]
let ``route collect produces valid json`` () =
    ensureOutDir ()
    let outFile = Path.Combine(outDir, "routes.json") |> Path.GetFullPath
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code, stdout, err = runTool ($"--route-collect --input {inputSpec} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)
    let root = parseJsonRoot (File.ReadAllText(outFile))
    Assert.That(hasJsonProperty root "routes", Is.True)
    Assert.That(root.GetProperty("routes").GetArrayLength(), Is.GreaterThan(0))

[<Test>]
let ``merge with two specs produces output file`` () =
    ensureOutDir ()
    let outFile = Path.Combine(outDir, "merged.yaml") |> Path.GetFullPath
    let inputA = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let inputB = Path.Combine(repoRoot, "Samples", "petstore-extended.yaml")
    let code, stdout, err =
        runTool ($"--merge --input {inputA} --input {inputB} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)
    Assert.That(File.ReadAllText(outFile).Length, Is.GreaterThan(0))

[<Test>]
let ``scissors with tag filter produces output file`` () =
    ensureOutDir ()
    let outFile = Path.Combine(outDir, "cut-pets.yaml") |> Path.GetFullPath
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code, stdout, err =
        runTool ($"--scissors --input {inputSpec} --out {outFile} --include-tag pets")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)
    let content = File.ReadAllText(outFile)
    Assert.That(content.Contains("pets"), Is.True)

[<Test>]
let ``lint format json produces valid json array`` () =
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code, stdout, err = runTool ($"--lint --input {inputSpec} --format json")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0).Or.EqualTo(1), err)
    let root = parseJsonRoot stdout
    Assert.That(root.ValueKind, Is.EqualTo(JsonValueKind.Array))
    if root.GetArrayLength() > 0 then
        let first = root[0]
        Assert.That(hasJsonProperty first "rule", Is.True)
        Assert.That(hasJsonProperty first "severity", Is.True)
        Assert.That(hasJsonProperty first "message", Is.True)
        Assert.That(hasJsonProperty first "location", Is.True)

[<Test>]
let ``version outputs version string and exits 0`` () =
    let code, stdout, err = runTool "--version"
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(stdout.Trim().Contains("0.9.0"), Is.True, $"Expected version 0.9.0 in: {stdout}")

[<Test>]
let ``lint exits 0 on clean spec`` () =
    let cleanSpec =
        writeTempSpec "lint-clean.yaml"
            """
openapi: 3.0.3
info:
  title: Clean API
  version: 1.0.0
paths:
  /pets:
    get:
      operationId: listPets
      summary: List pets
      parameters:
        - name: limit
          in: query
          description: Maximum number of pets to return
          schema:
            type: integer
      responses:
        '200':
          description: A list of pets
components:
  schemas:
    Pet:
      type: object
      properties:
        id:
          type: integer
          description: Pet identifier
"""

    let code, stdout, err = runTool ($"--lint --input {cleanSpec}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), $"stdout={stdout} stderr={err}")

[<Test>]
let ``lint exits 1 on spec with errors`` () =
    let errorSpec =
        writeTempSpec "lint-errors.yaml"
            """
openapi: 3.0.3
info:
  title: Error API
  version: 1.0.0
paths:
  /items:
    get:
      summary: List items without operation id
      responses:
        '200':
          description: OK
"""

    let code, stdout, err = runTool ($"--lint --input {errorSpec}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(1), $"stdout={stdout} stderr={err}")
    Assert.That(stdout.Contains("missing-operation-id"), Is.True)

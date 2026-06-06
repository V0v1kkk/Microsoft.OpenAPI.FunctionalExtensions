[<NUnit.Framework.NonParallelizable>]
module Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tests.Tool

open System
open System.IO
open System.Text
open NUnit.Framework

let private repoRoot =
    Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))

let private resolveToolDll () =
    let basePath = Path.Combine(repoRoot, "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool", "bin")
    [ "Debug"; "Release" ]
    |> List.tryPick (fun cfg ->
        let path =
            Path.Combine(basePath, cfg, "net9.0", "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool.dll")
        if File.Exists path then Some path else None)
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

[<SetUp>]
let requireGraphvizForSvgTests () =
    let testName = TestContext.CurrentContext.Test.Name
    if testName.Contains("dot") then
        ()
    else
        Assume.That(isGraphvizAvailable (), Is.True, "Graphviz (dot) is not available on PATH")

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

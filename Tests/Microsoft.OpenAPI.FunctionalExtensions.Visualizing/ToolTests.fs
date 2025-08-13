module Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tests.Tool

open System
open System.IO
open NUnit.Framework

let runTool args =
    // Resolve absolute paths so test working directory does not matter
    let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))
    let toolProj = Path.Combine(repoRoot, "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool", "Microsoft.OpenAPI.FunctionalExtensions.Visualizing.Tool.fsproj")
    let buildCmd = $"dotnet build {toolProj} -c Debug -v minimal"
    let runCmd = $"dotnet run --no-build --project {toolProj} -- {args}"
    let cmd = buildCmd + " && " + runCmd
    let psi = Diagnostics.ProcessStartInfo("bash", "-lc \"" + cmd + "\"")
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    use p = Diagnostics.Process.Start(psi)
    let exited = p.WaitForExit(20000)
    if not exited then
        try p.Kill(true) with _ -> ()
        // use 124 as timeout code (conventional)
        124, p.StandardOutput.ReadToEnd(), p.StandardError.ReadToEnd()
    else
        p.ExitCode, p.StandardOutput.ReadToEnd(), p.StandardError.ReadToEnd()

[<Test>]
let ``schema svg from petstore`` () =
    let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))
    let outDir = Path.Combine(repoRoot, "out")
    Directory.CreateDirectory(outDir) |> ignore
    let outFile = Path.Combine(outDir, "schema.svg") |> Path.GetFullPath
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code,stdout,err = runTool ($"--schema-svg --input {inputSpec} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)

[<Test>]
let ``route svg from petstore`` () =
    let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))
    let outDir = Path.Combine(repoRoot, "out")
    Directory.CreateDirectory(outDir) |> ignore
    let outFile = Path.Combine(outDir, "routes.svg") |> Path.GetFullPath
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code,stdout,err = runTool ($"--route-svg --input {inputSpec} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    Assert.That(File.Exists(outFile), Is.True)

[<Test>]
let ``schema dot contains array element type`` () =
    let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "..", ".."))
    let outDir = Path.Combine(repoRoot, "out")
    Directory.CreateDirectory(outDir) |> ignore
    let outFile = Path.Combine(outDir, "schema.dot") |> Path.GetFullPath
    let inputSpec = Path.Combine(repoRoot, "Samples", "petstore.yaml")
    let code,stdout,err = runTool ($"--schema-svg --dot --input {inputSpec} --out {outFile}")
    TestContext.WriteLine(stdout)
    TestContext.WriteLine(err)
    Assert.That(code, Is.EqualTo(0), err)
    let dot = File.ReadAllText(outFile)
    Assert.That(dot.Contains("array[Pet]"), Is.True, "DOT should include array[Pet] label")
open System.Text.RegularExpressions
#r "paket:
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.JavaScript.Yarn
nuget Fake.Core.Target //"
#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.JavaScript
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open System.IO
open System

Target.initEnvironment ()

let buildOutput = "./output/"
let webRoot = Path.Combine(buildOutput, "WebRoot")
let javascriptOutput = Path.Combine(webRoot, "scripts")

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "src/**/obj"
    ++ buildOutput
    |> Shell.cleanDirs 
)

Target.create "Restore" (fun _ ->
  !! "**/*.*proj"
  -- "**/.fable/**"
  -- "**/node_modules/**"
  |> Seq.iter (DotNet.restore id)

  Yarn.install (fun bo -> { bo with WorkingDirectory = "./DogPark.Client/" })
)

Target.create "Build" (fun _ ->
    Trace.log "Building JS dependencies..."
    Yarn.exec (sprintf "fable-splitter DogPark.Client -o %s --commonjs" javascriptOutput) id

    Trace.log "Building .NET projects..."
    !! "**/*.*proj"
    -- "**/.fable/**"
    -- "**/node_modules/**"
    -- "**/DogPark.Client/**"
    |> Seq.iter (
        DotNet.build (fun bo -> 
          { bo with 
              NoRestore = true
              OutputPath = Some buildOutput}))

    Trace.log "Removing the annoying language resources..."
    Directory.GetDirectories(buildOutput)
    |> Seq.filter (fun dir -> Regex.IsMatch(dir, @"[\\\\/][a-z]{2}(?:-[A-z]{2,})?$"))
    |> Shell.deleteDirs         
)

Target.create "Run" (fun _ ->

  File.checkExists "mariadb.txt"

  // could potentially be used to inject arguments. But, if they have access to your filesystem, you're fucked anyway ¯\_(ツ)_/¯
  let connection = File.readLine "mariadb.txt"

  CreateProcess.fromRawCommandLine (Path.Combine(buildOutput, "server.exe")) (sprintf "--environment Development --MariaDB \"%s\"" connection )
  |> CreateProcess.withWorkingDirectory buildOutput
  |> Proc.run
  |> ignore
)

Target.create "Publish" (fun _ ->
  let publishPath = Environment.environVarOrDefault "output" "published/"
  Directory.ensure publishPath
  Shell.copyRecursive buildOutput publishPath true
  |> Trace.logItems "Publish! - "
)

Target.create "All" ignore

"Clean"
  ==> "Restore"
  ==> "Build"
  ==> "Run"
  ==> "All"

"Build"
  ==> "Publish"

Target.runOrDefault "All"

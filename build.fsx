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

Target.create "Restore" (fun _ ->
  !! "**/*.*proj"
  -- "**/.fable/**"
  -- "**/node_modules/**"
  |> Seq.iter (DotNet.restore id)

  Yarn.install (fun bo -> { bo with WorkingDirectory = "./DogPark.Client/" })
)

let build() =
    Trace.log "Building JS dependencies..."
    Yarn.exec (sprintf "fable-splitter DogPark.Client --outDir %s --allFiles" javascriptOutput) id

    Trace.log @"Fixing imports... >:\"
    !! (sprintf "%s/**/*.js" javascriptOutput)
    |> Seq.iter (fun path ->
      path
      |> sprintf "Fixing %s"
      |> Trace.log

      let data = File.ReadAllText(path)
      let fixedData = Regex.Replace(data, @"(^\s*import .*? from\s*"".*?)"";", @"$1.js"";", RegexOptions.Multiline)
      File.WriteAllText(path, fixedData)
    )

    let configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault "Configuration" DotNet.Debug
    Trace.logf "Building .NET projects (%O)..." configuration
    !! "**/*.*proj"
    -- "**/.fable/**"
    -- "**/node_modules/**"
    -- "**/DogPark.Client/**"
    |> Seq.iter (
        DotNet.build (fun bo ->
          { bo with
              Configuration = configuration
              NoRestore = true
              OutputPath = Some buildOutput}))

    Trace.log "Removing the annoying language resources..."
    Directory.GetDirectories(buildOutput)
    |> Seq.filter (fun dir -> Regex.IsMatch(dir, @"[\\\\/][a-z]{2}(?:-[A-z]{2,})?$"))
    |> Shell.deleteDirs

Target.create "Build" (fun _ ->
  build()
)

Target.create "Clean" (fun _ ->
  !! "**/bin"
  ++ "**/obj"
  -- "**/.fable/**"
  -- "**/node_modules/**"
  ++ buildOutput
  |> Shell.cleanDirs
)

Target.create "CleanedBuild" (fun _ ->
  build()
)

Target.create "Run" (fun _ ->
  CreateProcess.fromRawCommandLine (Path.Combine(buildOutput, "server.exe")) String.Empty
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

"Restore"
  ==> "Build"
  ==> "Run"
  ==> "All"

"Clean"
  ==> "Restore"
  ==> "CleanedBuild"
  ==> "Publish"

Target.runOrDefault "All"

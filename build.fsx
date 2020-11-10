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

let buildOutput = "output/"
let webRoot = Path.Combine(buildOutput, "wwwroot")
let javascriptOutput = Path.Combine(webRoot, "scripts")

let restore() =
  !! "**/*.*proj"
  -- "**/.fable/**"
  -- "**/node_modules/**"
  |> Seq.iter (DotNet.restore id)

  Yarn.install (fun bo -> { bo with WorkingDirectory = "DogPark.Client/" })
Target.create "Restore" (fun _ ->
  restore()
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
    -- "**/Bolero*/**"
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

let boleroBuild() =
  let configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault "Configuration" DotNet.Debug
  !! "**/Bolero*/*.*proj"
    |> Seq.iter (fun projectPath ->
      projectPath
      |> DotNet.publish (fun po ->
          { po with
              Configuration = configuration
              NoRestore = true
              OutputPath = Some buildOutput
          })
    )

  // for some reason, IsTransformWebConfigDisabled is ignored. MS can fuck right off. I'd sooner lick the nasty side of a toilet seat
  // before trying to use IIS again
  printfn "Removing stupid 'web.config' files since MSBuild refuses to not generate them"
  !! (sprintf "%s/**/web.config" buildOutput)
  |> Seq.iter File.Delete

Target.create "BoleroBuild" (fun _ ->
  boleroBuild()
)

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
  restore()
  boleroBuild()
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
  ==> "BoleroBuild"
  ==> "Build"
  ==> "Run"
  ==> "All"

"Clean"
  ==> "CleanedBuild"
  ==> "Publish"

Target.runOrDefault "All"

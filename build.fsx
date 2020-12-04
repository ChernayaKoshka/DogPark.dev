#r "paket:
nuget Fake.DotNet.Cli
nuget Fake.IO.FileSystem
nuget Fake.Core.Target //"
#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

Target.initEnvironment ()

let configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault "Configuration" DotNet.Debug

Target.create "Clean" (fun _ ->
    match configuration with
    | DotNet.Debug ->
      Trace.log "Skipping clean because it's a debug build"
    | _ ->
      !! "**/bin"
      ++ "**/obj"
      |> Shell.cleanDirs
)

let sass = "tools/dart-sass/sass.bat"
Target.create "BuildCss" (fun _ ->
  let exitCode = Shell.Exec(sass, "--no-source-map DogPark.Client/bulma/style.scss:DogPark.Client/wwwroot/css/style.css")
  if exitCode <> 0 then raise <| exn(sprintf "Exit code from '%s' was %d!" sass exitCode)
  else ()
)

Target.create "Build" (fun _ ->
    // needs to use var or somethin' to check build mode
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun parms -> { parms with Configuration = configuration }))
)

Target.create "ApplyLoadingHack" (fun _ ->
    // https://medium.com/@stef.heyenrath/show-a-loading-progress-indicator-for-a-blazor-webassembly-application-ea28595ff8c1
    !! "**/blazor.webassembly.js"
    |> Seq.iter (fun path ->
      let text = File.readAsString path
      let newText = text.Replace(@"return r.loadResource(o,t(o),e[o],n)", @"var p = r.loadResource(o,t(o),e[o],n); p.response.then((x) => { if (typeof window.loadResourceCallback === 'function') { window.loadResourceCallback(Object.keys(e).length, o, x);}}); return p;")
      File.writeString false path newText
    )
)

Target.create "Run" (fun _ ->
  let configuration = Environment.environVarOrDefault "Configuration" "Debug"
  // I need to find a better way
  let server = !! (sprintf "DogPark.Server/bin/%s/*/Server.exe" configuration) |> Seq.head
  Shell.Exec(server, dir = "DogPark.Server")
  |> ignore
)

Target.create "CleanPublishDir" (fun _ ->
  if Shell.testDir "Published" then
    Shell.cleanDir "Published"
)

Target.create "CreatePublishArtifacts" (fun _ ->
  Trace.log "Publishing"
  // deliberately a blank string to force the entire solution to be published instead of publishing each individual project
  DotNet.publish (fun parms -> { parms with Configuration = configuration; OutputPath = Some "Published" }) ""

  Shell.deleteDir "Published/BlazorDebugProxy"

  // stupid language resource that .NET thinks we need in every single application ever published
  [ "cs"; "de"; "es"; "fr"; "it"; "ja"; "ko"; "pl"; "pt-BR"; "ru"; "tr"; "zh-Hans"; "zh-Hant" ]
  |> Seq.map (sprintf "Published/%s")
  |> Shell.deleteDirs

  File.delete "Published/web.config"

)

Target.create "Publish" ignore
Target.create "All" ignore

"Clean"
  ?=> "BuildCss"
  ==> "Build"
  ?=> "ApplyLoadingHack"
  ==> "Run"

"Clean"
  ==> "CleanPublishDir"
  ?=> "BuildCss"
  ==> "CreatePublishArtifacts"
  ?=> "ApplyLoadingHack"
  ==> "Publish"

"Clean" ==> "Build"
"CleanPublishDir" ==> "CreatePublishArtifacts"
"CreatePublishArtifacts" ==> "Publish"
"Build" ==> "Run"

Target.runOrDefault "All"

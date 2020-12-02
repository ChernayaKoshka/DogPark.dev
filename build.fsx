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

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    |> Shell.cleanDirs
)

Target.create "Build" (fun _ ->
    let configuration = DotNet.BuildConfiguration.fromEnvironVarOrDefault "Configuration" DotNet.Debug
    // needs to use var or somethin' to check build mode
    !! "**/*.*proj"
    |> Seq.iter (DotNet.build (fun parms -> { parms with Configuration = configuration }))

    // https://medium.com/@stef.heyenrath/show-a-loading-progress-indicator-for-a-blazor-webassembly-application-ea28595ff8c1
    !! "**/blazor.webassembly.js"
    |> Seq.iter (fun path ->
      let text = File.readAsString path
      let newText = text.Replace(@"return r.loadResource(o,t(o),e[o],n)", @"var p = r.loadResource(o,t(o),e[o],n); p.response.then((x) => { if (typeof window.loadResourceCallback === 'function') { window.loadResourceCallback(Object.keys(e).length, o, x);}}); return p;")
      File.writeString false path newText
    )
)

Target.create "Run" (fun _ ->
  // I need to find a better way
  let configuration = Environment.environVarOrDefault "Configuration" "Debug"
  let server = !! (sprintf "**/%s/**/Server.exe" configuration) |> Seq.head
  Shell.Exec(server, dir = "DogPark.Server")
  |> ignore
)

Target.create "All" ignore

"Clean"
  ==> "Build"
  ==> "Run"
  ==> "All"

Target.runOrDefault "All"

namespace DogPark.Client

open Microsoft.AspNetCore.Components.WebAssembly.Hosting
open Bolero.Remoting.Client
open Microsoft.Extensions.DependencyInjection
open Blazored.LocalStorage

module Program =

    [<EntryPoint>]
    let Main args =
        let builder = WebAssemblyHostBuilder.CreateDefault(args)
        builder.RootComponents.Add<Main.MyApp>(":root")
        builder.Services.AddRemoting(builder.HostEnvironment) |> ignore
        builder.Services.AddHttpClient() |> ignore
        builder.Services.AddBlazoredLocalStorage() |> ignore
        builder.Build().RunAsync() |> ignore
        0

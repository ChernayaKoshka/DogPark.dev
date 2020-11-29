module DogPark.Client.Main

open Elmish
open Bolero
open Bolero.Html
open Microsoft.Extensions.DependencyInjection;
open EasyHttp
open System
open System.Net.Http
open System.IO
open System.Threading.Tasks
open DogPark.Shared

type Api =
    {
        [<Method("GET")>]
        [<Path("ping")>]
        Ping: unit -> Task<string>
    }
    with
        static member BaseUri = Uri("http://localhost:7777/api/v1/")

type Model =
    {
        Api: Api
        ClientFactory: IHttpClientFactory
        PingResult: string
    }
type Message =
    | Ping
    | SetPingText of string

let initModel (clientFactory: IHttpClientFactory) =
    let client = clientFactory.CreateClient(BaseAddress = Api.BaseUri)

    let api =
        match makeApi<Api> client with
        | Ok api -> api
        | Error err -> failwith err
    {
        Api = api
        ClientFactory = clientFactory
        PingResult = "Press the Ping! button"
    }, Cmd.none

let update message model =
    match message with
    | SetPingText text ->
        { model with
            PingResult = text
        }, Cmd.none
    | Ping ->
        model, Cmd.OfTask.perform model.Api.Ping () SetPingText

let view model dispatch =
    div [ ] [
        p [ ] [ text "Hello, world!" ]
        text $"Ping Result: {model.PingResult}"
        p [ ] [ button [ on.click (fun _ -> printfn "Dispatching"; dispatch Ping); ] [ text "Ping!" ] ]
    ]

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let hcf = this.Services.GetService<IHttpClientFactory>()
        Program.mkProgram (fun _ -> initModel hcf) update view
        |> Program.withConsoleTrace

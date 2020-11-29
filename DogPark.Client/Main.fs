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
open FSharp.Control.Tasks

type Api =
    {
        [<Method("GET")>]
        [<Path("ping")>]
        Ping: unit -> Task<string>

        [<Method("GET")>]
        [<Path("article")>]
        Articles: unit -> Task<ArticleDetails seq>
    }
    with
        static member BaseUri = Uri("http://localhost:7777/api/v1/")

type Page =
    | Home
    | Article of id: uint32
    | Articles
    | ArticlesNoWrapper

type Model =
    {
        Page: Page
        Api: Api
        ClientFactory: IHttpClientFactory
        PingResult: string

        ArticlesList: ArticleDetails seq
    }
type Message =
    | SetPage of Page
    | Ping
    | SetPingText of string
    | GetArticlesList
    | GetArticlesListNoWrapper
    | GotArticlesList of ArticleDetails seq
    | DoNothing

let router = Router.infer SetPage (fun m -> m.Page)

let initModel (clientFactory: IHttpClientFactory) =
    let client = clientFactory.CreateClient(BaseAddress = Api.BaseUri)

    let api =
        match makeApi<Api> client with
        | Ok api -> api
        | Error err -> failwith err
    {
        Page = Home
        Api = api
        ClientFactory = clientFactory
        PingResult = "Press the Ping! button"
        ArticlesList = Seq.empty
    }, Cmd.none

let update message model =
    match message with
    | SetPage page ->
        let nextCmd =
            match page with
            | Articles ->
                Cmd.ofMsg GetArticlesList
            | ArticlesNoWrapper ->
                Cmd.ofMsg GetArticlesListNoWrapper
            | _ ->
                Cmd.none
        { model with
            Page = page
        }, nextCmd
    | SetPingText text ->
        { model with
            PingResult = text
        }, Cmd.none
    | Ping ->
        model, Cmd.OfTask.perform model.Api.Ping () SetPingText
    | GetArticlesListNoWrapper ->
        model, Cmd.OfTask.either model.Api.Articles () GotArticlesList (fun err -> printfn "%A" err; DoNothing)
    | GetArticlesList ->
        model,
        Cmd.OfTask.either
            (fun () -> task { return! model.Api.Articles() })
            ()
            (fun l -> printfn "got %A" l; GotArticlesList l)
            (fun err -> printfn "%A" err; DoNothing)
    | GotArticlesList articles ->
        { model with
            ArticlesList = articles
        }, Cmd.none
    | DoNothing -> model, Cmd.none

let articlesView model dispatch =
    div [ ] [
        ul [ ] [
            forEach
                model.ArticlesList
                (fun al ->
                    li [ ] [
                        text $"{al.Author} @ {al.Created} - "
                        a [ router.HRef (Article al.Id) ] [ text al.Headline ]
                    ]
                )
        ]
    ]

let homeView model dispatch =
    div [ ] [
        p [ ] [ text "Hello, world!" ]
        text $"Ping Result: {model.PingResult}"
        p [ ] [ button [ on.click (fun _ -> printfn "Dispatching"; dispatch Ping); ] [ text "Ping!" ] ]
    ]

let view model dispatch =
    div [ ] [
        p [ ] [ model.Page |> string |> text ]
        hr [ ]

        cond model.Page <| function
        | Home -> homeView model dispatch
        | Article id -> homeView model dispatch
        | Articles -> articlesView model dispatch
        | ArticlesNoWrapper -> articlesView model dispatch

        p [ ] [
            a [ router.HRef Home ] [ text "Home" ]
            a [ router.HRef (Article 1u) ] [ text "Article 1" ]
            a [ router.HRef Articles ] [ text "Articles" ]
            a [ router.HRef ArticlesNoWrapper ] [ text "ArticlesNoWrapper" ]
        ]
    ]

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let hcf = this.Services.GetService<IHttpClientFactory>()
        Program.mkProgram (fun _ -> initModel hcf) update view
        #if DEBUG
        |> Program.withConsoleTrace
        #endif
        |> Program.withRouter router

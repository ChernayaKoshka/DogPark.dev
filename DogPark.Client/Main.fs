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
open FSharp.Control.Tasks.V2.ContextInsensitive

type Api =
    {
        [<Method("GET")>]
        [<Path("ping")>]
        Ping: unit -> Task<string>

        [<Method("GET")>]
        [<Path("article")>]
        Articles: unit -> Task<ArticleDetails seq>

        [<Method("GET")>]
        [<Path("article/{Id}")>]
        Article: {| Id: uint32 |} -> Task<Article>
    }
    with
        static member BaseUri = Uri("http://localhost:7777/api/v1/")

type Page =
    | Home
    | Article of id: uint32
    | Articles

type Model =
    {
        Page: Page
        Api: Api
        ClientFactory: IHttpClientFactory
        PingResult: string

        ArticlesList: ArticleDetails seq
        Article: Article option
    }
type Message =
    | SetPage of Page
    | Ping
    | SetPingText of string
    | GetArticlesList
    | GotArticlesList of ArticleDetails seq
    | GetArticle of id: uint32
    | GotArticle of Article
    | DoNothing

let router = Router.infer SetPage (fun m -> m.Page)

let initModel (clientFactory: IHttpClientFactory) =
    let client = clientFactory.CreateClient(BaseAddress = Api.BaseUri)

    let api =
        match makeApi<Api> Api.BaseUri client with
        | Ok api -> api
        | Error err -> failwith err
    {
        Page = Home
        Api = api
        ClientFactory = clientFactory
        PingResult = "Press the Ping! button"
        ArticlesList = Seq.empty
        Article = None
    }, Cmd.none

let update message model =
    match message with
    | SetPage page ->
        let nextCmd =
            match page with
            | Articles ->
                Cmd.ofMsg GetArticlesList
            | Article id ->
                Cmd.ofMsg (GetArticle id)
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
    | GetArticlesList ->
        model,
        Cmd.OfTask.either
            model.Api.Articles
            ()
            GotArticlesList
            (fun err -> printfn "%A" err; DoNothing)
    | GotArticlesList articles ->
        { model with
            ArticlesList = articles
        }, Cmd.none
    | GetArticle id ->
        model,
        Cmd.OfTask.either
            model.Api.Article
            {| Id = id |}
            GotArticle
            (fun err -> printfn "%A" err; DoNothing)
    | GotArticle article ->
        { model with
            Article = Some article
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

type Article = Template<"./wwwroot/article.html">
let articleView model dispatch =
    div [ ] [
        cond model.Article <| function
        | Some article ->
            Article()
                .Title(article.Details.Headline)
                .Subtitle($"Author: {article.Details.Author} @ {article.Details.Created}")
                .Content(RawHtml article.HtmlBody)
                .Elt()
        | None ->
            text "Loading..."
    ]

let homeView model dispatch =
    div [ ] [
        p [ ] [ text "Hello, world!" ]
        text $"Ping Result: {model.PingResult}"
        p [ ] [ button [ on.click (fun _ -> printfn "Dispatching"; dispatch Ping); ] [ text "Ping!" ] ]
    ]

let view model dispatch =
    div [ attr.``class`` "container" ] [
        cond model.Page <| function
        | Home -> homeView model dispatch
        | Article id -> articleView model dispatch
        | Articles -> articlesView model dispatch
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

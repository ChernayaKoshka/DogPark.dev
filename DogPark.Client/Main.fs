module DogPark.Client.Main

// fsharplint:disable CanBeReplacedWithComposition

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

        [<Path("account/login")>]
        Login: LoginModel -> Task<AccountDetailsResponse>

        [<Path("account/logout")>]
        Logout: unit -> Task<GenericResponse>

        [<Path("account/changepassword")>]
        ChangePassword: ChangePasswordModel -> Task<GenericResponse>

        [<Method("GET")>]
        [<Path("account/details")>]
        AccountDetails: unit -> Task<AccountDetailsResponse>

    }
    with
        #if DEBUG
        static member BaseUri = Uri("http://localhost:7777/api/v1/")
        #else
        static member BaseUri = Uri("https://dogpark.dev/api/v1/")
        #endif

type Page =
    | Home
    | Article of id: uint32
    | Articles
    | Login

type Model =
    {
        Page: Page
        Api: Api
        ClientFactory: IHttpClientFactory
        PingResult: string

        ArticlesList: ArticleDetails seq
        Article: Article option
        Login: LoginModel option
        AccountDetails: AccountDetails option
    }
type Message =
    | SetPage of Page
    | Ping
    | SetPingText of string

    | GetAccountDetails
    | GotAccountDetails of AccountDetailsResponse

    | GetArticlesList
    | GotArticlesList of ArticleDetails seq
    | GetArticle of id: uint32
    | GotArticle of Article

    | Login
    | LoginResult of AccountDetailsResponse
    | Logout
    | LogoutResult of GenericResponse

    | SetUsername of string
    | SetPassword of string
    | DoNothing

let router = Router.infer SetPage (fun m -> m.Page)

let initModel (clientFactory: IHttpClientFactory) =
    let client = clientFactory.CreateClient(BaseAddress = Api.BaseUri)

    let api =
        match makeApi<Api> Api.BaseUri jsonOptions client with
        | Ok api -> api
        | Error err -> failwith err

    {
        Page = Home
        Api = api
        ClientFactory = clientFactory
        PingResult = "Press the Ping! button"
        ArticlesList = Seq.empty
        Article = None
        Login = None
        AccountDetails = None
    }, Cmd.ofMsg GetAccountDetails

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

    | GetAccountDetails ->
        model,
        Cmd.OfTask.either
            model.Api.AccountDetails
            ()
            GotAccountDetails
            (fun err -> printfn "%A" err; DoNothing)
    | GotAccountDetails details ->
        { model with
            AccountDetails = details.Details
        }, Cmd.none

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

    | Login ->
        match model.Login with
        | Some login ->
            model,
            Cmd.OfTask.either
                model.Api.Login
                login
                LoginResult
                (fun err -> printfn "%A" err; DoNothing)
        | None ->
            model, Cmd.none
    | LoginResult result ->
        printfn "%A" result
        { model with
            AccountDetails = result.Details
        }, Cmd.ofMsg (SetPage Home)
    | Logout ->
        model,
        Cmd.OfTask.either
            model.Api.Logout
            ()
            LogoutResult
            (fun err -> printfn "%A" err; DoNothing)
    | LogoutResult result ->
        if result.Success then
            { model with AccountDetails = None }, Cmd.none
        else
            printfn "%A" result
            model, Cmd.none

    | SetUsername username ->
        let login =
            match model.Login with
            | Some login ->
                { login with Username = username }
            | None ->
                { Username = username; Password = String.Empty }
        { model with Login = Some login }, Cmd.none
    | SetPassword password ->
        let login =
            match model.Login with
            | Some login ->
                { login with Password = password }
            | None ->
                { Username = String.Empty; Password = password }
        { model with Login = Some login }, Cmd.none
    | DoNothing ->
        model, Cmd.none

type View = Template<"./wwwroot/templates/realIndex.html">
let articlesView model dispatch =
    View()
        .Head(Empty)
        .LoginoutButtonLink(
            if model.AccountDetails.IsSome then
                null
            else
                router.Link Page.Login
        )
        .LoginoutButtonText(
            if model.AccountDetails.IsSome then
                "Logout"
            else
                "Login"
        )
        .LoginoutButtonClicked((fun _ ->
            if model.AccountDetails.IsSome then
                dispatch Logout
            )
        )
        .HelloText(
            match model.AccountDetails with
            | Some details ->
                p [ attr.``class`` "navbar-item is-size-5" ] [
                    text $"Hello, {details.Username}"
                ]
            | None -> Empty
        )
        .Content(
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
        )
        .Scripts(Empty)
        .Elt()

type Article = Template<"./wwwroot/templates/article.html">
let articleView model dispatch =
    View()
        .Head(link [ attr.rel "stylesheet"; attr.href "css/vs.css" ])
        .LoginoutButtonLink(
            if model.AccountDetails.IsSome then
                null
            else
                router.Link Page.Login
        )
        .LoginoutButtonText(
            if model.AccountDetails.IsSome then
                "Logout"
            else
                "Login"
        )
        .LoginoutButtonClicked((fun _ ->
            if model.AccountDetails.IsSome then
                dispatch Logout
            )
        )
        .HelloText(
            match model.AccountDetails with
            | Some details ->
                p [ attr.``class`` "navbar-item is-size-5" ] [
                    text $"Hello, {details.Username}"
                ]
            | None -> Empty
        )
        .Content(
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
        )
        .Scripts(
            concat [
                script [ attr.src "scripts/highlight.pack.js" ] [ ]
                cond model.Article <| function
                | Some _ ->
                    concat [
                        script [ ] [
                            text
                                """
                                var blocks = document.querySelectorAll('pre code:not(.hljs)');
                                Array.prototype.forEach.call(blocks, hljs.highlightBlock);
                                """
                        ]
                    ]
                | None ->
                    empty
            ]
        )
        .Elt()

let homeView model dispatch =
    View()
        .Head(Empty)
        .LoginoutButtonLink(
            if model.AccountDetails.IsSome then
                null
            else
                router.Link Page.Login
        )
        .LoginoutButtonText(
            if model.AccountDetails.IsSome then
                "Logout"
            else
                "Login"
        )
        .LoginoutButtonClicked((fun _ ->
            if model.AccountDetails.IsSome then
                dispatch Logout
            )
        )
        .HelloText(
            match model.AccountDetails with
            | Some details ->
                p [ attr.``class`` "navbar-item is-size-5" ] [
                    text $"Hello, {details.Username}"
                ]
            | None -> Empty
        )
        .Content(
            div [ ] [
                p [ ] [ text "Hello, world!" ]
                text $"Ping Result: {model.PingResult}"
                p [ ] [ button [ on.click (fun _ -> printfn "Dispatching"; dispatch Ping); ] [ text "Ping!" ] ]
            ]
        )
        .Scripts(Empty)
        .Elt()

type LoginView = Template<"./wwwroot/templates/login.html">
let loginView model dispatch =
    let login = Option.defaultValue LoginModel.Default model.Login

    (*
        <button
            type="submit"
            class="button is-primary"
            onclick="${Login}"
            ${LoginDisabled}>Login</button>
    *)
    let loginButton =
        button [
            attr.``type`` "submit"
            attr.``class`` "button is-primary is-pulled-right"
            on.click (fun _ -> dispatch Login)
            if login.IsValid() |> not then attr.disabled ""
        ] [
            text "Login"
        ]

    View()
        .Head(Empty)
        .LoginoutButtonLink(
            if model.AccountDetails.IsSome then
                null
            else
                router.Link Page.Login
        )
        .LoginoutButtonText(
            if model.AccountDetails.IsSome then
                "Logout"
            else
                "Login"
        )
        .LoginoutButtonClicked((fun _ ->
            if model.AccountDetails.IsSome then
                dispatch Logout
            )
        )
        .HelloText(
            match model.AccountDetails with
            | Some details ->
                p [ attr.``class`` "navbar-item is-size-5" ] [
                    text $"Hello, {details.Username}"
                ]
            | None -> Empty
        )
        .Content(
            LoginView()
                .Username(login.Username, (fun s -> dispatch (SetUsername s)))
                .Password(login.Password, (fun s -> dispatch (SetPassword s)))
                .Keyup(fun kea -> if kea.Key = "Enter" && login.IsValid() then dispatch Login)
                .LoginButton(loginButton)
                .Elt()
        )
        .Scripts(Empty)
        .Elt()

let view model dispatch =
    cond model.Page <| function
    | Home ->
        homeView model dispatch
    | Article id ->
        articleView model dispatch
    | Articles ->
        articlesView model dispatch
    | Page.Login ->
        loginView model dispatch

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let hcf = this.Services.GetService<IHttpClientFactory>()
        Program.mkProgram (fun _ -> initModel hcf) update view
        #if DEBUG
        |> Program.withConsoleTrace
        #endif
        |> Program.withRouter router

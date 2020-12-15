module DogPark.Client.Main

open Elmish
open Bolero
open Bolero.Html
open Microsoft.Extensions.DependencyInjection;
open EasyHttp
open System
open System.Net.Http
open System.IO
open System.Threading
open System.Threading.Tasks
open DogPark.Shared
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Net.Http.Headers
open Blazored.LocalStorage
open DogPark.Client.Views
open Microsoft.JSInterop

type Page =
    | [<EndPoint("/")>] Home
    | Article of id: uint32
    | Articles
    | Login
    | Editor

type Submodel =
    | Home
    | Login of Login.Model
    | ArticlesList of ArticlesList.Model
    | Article of Article.Model
    | Editor of Editor.Model

type SubmodelMsg =
    | LoginMsg of Login.Msg
    | ArticleMsg of Article.Msg
    | ArticlesListMsg of ArticlesList.Msg
    | EditorMsg of Editor.Msg

type Message =
    | SetPage of Page
    | ToggleBurger

    | LoginResult of LoginResponse
    | BeginRefreshToken

    | Logout
    | LogoutResult of GenericResponse

    | SetError of string option
    | DoNothing
    | SubmodelMsg of SubmodelMsg

let setError (o: #obj) =
    o
    |> string
    |> Some
    |> SetError

type Model =
    {
        Page: Page
        Submodel: Submodel

        Api: Api
        ApiClient: HttpClient
        ClientFactory: IHttpClientFactory
        LocalStorage: ILocalStorageService
        JSRuntime: IJSRuntime

        Username: string option
        ErrorMessage: string option
        BurgerActive: bool
    }

let router = Router.infer SetPage (fun m -> m.Page)

let initModel (clientFactory: IHttpClientFactory) (localStorage: ILocalStorageService) (jsRuntime: IJSRuntime) =
    let client = clientFactory.CreateClient(BaseAddress = Api.BaseUri)

    let api =
        match makeApi<Api> Api.BaseUri jsonOptions client with
        | Ok api -> api
        | Error err -> failwith err

    {
        Page = Page.Home
        Submodel = Submodel.Home

        Api = api
        ApiClient = client
        ClientFactory = clientFactory
        LocalStorage = localStorage
        JSRuntime = jsRuntime

        Username = None
        ErrorMessage = None
        BurgerActive = false
    },
    Cmd.OfTask.either
        (fun _ -> task {
            match! localStorage.ContainKeyAsync "JWT" with
            | true ->
                let! jwt = localStorage.GetItemAsStringAsync("JWT")
                client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", jwt)
                return BeginRefreshToken
            | false ->
                return DoNothing
        })
        ()
        id
        setError

let subMsg msg =
    Cmd.map msg >> Cmd.map SubmodelMsg

let update message model =
    match message with
    | SetPage page ->
        let page, submodel, nextCmd =
            match page with
            | Page.Home ->
                page, Submodel.Home, Cmd.none
            | Page.Article id ->
                let submodel, nextCmd = Article.init model.Api id
                page, Submodel.Article submodel, subMsg ArticleMsg nextCmd
            | Page.Articles ->
                let submodel, nextCmd = ArticlesList.init model.Api
                page, Submodel.ArticlesList submodel, subMsg ArticlesListMsg nextCmd
            | Page.Login ->
                let submodel, nextCmd = Login.init model.Api
                page, Submodel.Login submodel, subMsg LoginMsg nextCmd
            | Page.Editor ->
                match model.Username with
                | Some _ ->
                    let submodel, nextCmd = Editor.init model.Api model.JSRuntime
                    page, Submodel.Editor submodel, subMsg EditorMsg nextCmd
                | None ->
                    Page.Home, Submodel.Home, Cmd.ofMsg (setError "You are not authorized to view that page.")
        { model with
            Page = page
            Submodel = submodel
        }, nextCmd
    | ToggleBurger ->
        { model with
            BurgerActive = not model.BurgerActive
        }, Cmd.none

    | LoginResult result ->
        match result.Details with
        | Some details ->
            let decoded = jwtDecodeNoVerify details.Jwt.AccessToken
            model.ApiClient.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", details.Jwt.AccessToken)
            { model with
                Username = Some details.Username
            }, Cmd.batch [
                Cmd.ofMsg (SetPage Page.Home)
                Cmd.OfTask.either
                    (fun () -> task {
                        printfn "Refreshing login token in %A" (decoded.ValidTo - DateTime.Now)
                        do! model.LocalStorage.SetItemAsync("JWT", details.Jwt.AccessToken)
                        do! Task.Delay(decoded.ValidTo - DateTime.Now)
                        return ()
                    })
                    ()
                    (fun _ -> BeginRefreshToken)
                    setError
            ]
        | None ->
            let message = Option.defaultValue "Sign in failed." result.Message
            { model with
                Username = None
            }, Cmd.batch [
                Cmd.ofMsg (setError message)
                Cmd.OfTask.attempt
                    (fun () -> task {
                        model.ApiClient.DefaultRequestHeaders.Authorization <- null
                        do! model.LocalStorage.RemoveItemAsync("JWT")
                    })
                    ()
                    setError
            ]
    | BeginRefreshToken ->
        model,
        Cmd.OfTask.either
            model.Api.RefreshToken
            ()
            LoginResult
            setError

    | Logout ->
        model,
        Cmd.OfTask.either
            (fun () -> task {
                model.ApiClient.DefaultRequestHeaders.Authorization <- null
                do! model.LocalStorage.RemoveItemAsync("JWT")
                return! model.Api.Logout()
            })
            ()
            LogoutResult
            setError
    | LogoutResult result ->
        if result.Success then
            { model with Username = None }, Cmd.ofMsg (SetPage Page.Home)
        else
            model, Cmd.ofMsg (setError result.Message)

    | SetError err ->
        { model with
            ErrorMessage = err
        }, Cmd.none

    | DoNothing ->
        model, Cmd.none

    | SubmodelMsg msg ->
        let next, nextCmd =
            match msg, model.Submodel with
            | ArticleMsg msg, Submodel.Article articleModel ->
                let next, nextCmd = Article.update articleModel msg
                Submodel.Article next, subMsg ArticleMsg nextCmd
            | ArticlesListMsg msg, Submodel.ArticlesList articlesListModel ->
                let next, nextCmd = ArticlesList.update articlesListModel msg
                Submodel.ArticlesList next, subMsg ArticlesListMsg nextCmd
            | LoginMsg (Login.LoginResult result), Submodel.Login loginModel ->
                Submodel.Login loginModel, Cmd.ofMsg (LoginResult result)
            | LoginMsg msg, Submodel.Login loginModel ->
                let next, nextCmd = Login.update loginModel msg
                Submodel.Login next, subMsg LoginMsg nextCmd
            | EditorMsg msg, Submodel.Editor editorModel ->
                match msg with
                | Editor.Msg.SetError err ->
                    Submodel.Editor editorModel, Cmd.ofMsg (SetError err)
                | Editor.Msg.SubmitResult result when result.Success ->
                    Submodel.Editor editorModel, Cmd.ofMsg (SetPage (Page.Article result.Id.Value))
                | msg ->
                    let next, nextCmd = Editor.update editorModel msg
                    Submodel.Editor next, subMsg EditorMsg nextCmd
            | msg, model ->
                failwithf "Somehow '%A' and '%A' ended up together! Or you forgot to add a new sobmodel" msg model
        { model with
            Submodel = next
        }, nextCmd

let homeView (baseView: View) model dispatch =
    baseView
        .Head(Empty)
        .Content(
            div [ ] [
                p [ ] [ text "Hello, world!" ]
            ]
        )
        .Scripts(Empty)
        .Elt()

let startNav dispatch username =
    concat [
        NavLink().Text("Articles").Link(router.Link Page.Articles).Elt()
        NavLink().Text("GitHub").Link("https://github.com/ChernayaKoshka/").Elt()
        match username with
        | Some _ ->
            NavLink().Text("Article Editor").Link(router.Link Page.Editor).Elt()
        | _ ->
            ()
    ]

let endNav dispatch username =
    match username with
    | Some username ->
        concat [
            p [ attr.``class`` "navbar-item is-size-5" ] [
                text $"Hello, {username}"
            ]
            NavButton()
                .Class("is-light")
                .Text("Logout")
                .Clicked(fun _ -> dispatch Logout)
                .Elt()
        ]
    | None ->
        NavButton()
            .Class("is-primary")
            .Text("Login")
            .Link(router.Link Page.Login)
            .Elt()

let view model dispatch =
    let baseView =
        View()
            .StartNav(startNav dispatch model.Username)
            .EndNav(endNav dispatch model.Username)
            .BurgerClicked(fun _ -> dispatch ToggleBurger)
            .IsActiveBurger(
                if model.BurgerActive then
                    "is-active"
                else
                    ""
            )
            .TopLevel(
                cond model.ErrorMessage <| function
                | Some err ->
                    ErrorNotification()
                        .Clicked(fun _ -> dispatch (SetError None))
                        .Content(text err)
                        .Elt()
                | _ ->
                    empty
            )

    match (model.Page, model.Submodel) with
    | Page.Home, Submodel.Home ->
        homeView baseView model dispatch
    | Page.Article id, Submodel.Article model ->
        Article.view baseView model (ArticleMsg >> SubmodelMsg >> dispatch)
    | Page.Articles, Submodel.ArticlesList model ->
        ArticlesList.view baseView model (ArticlesListMsg >> SubmodelMsg >> dispatch)
    | Page.Login, Submodel.Login model ->
        Login.view baseView model (LoginMsg >> SubmodelMsg >> dispatch)
    | Page.Editor, Submodel.Editor model ->
        Editor.view baseView model (EditorMsg >> SubmodelMsg >> dispatch)
    | page, model ->
        failwithf "'%A' and '%A' are not compatible or are not handled." page model

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let hcf = this.Services.GetService<IHttpClientFactory>()
        let localStorage = this.Services.GetService<ILocalStorageService>()
        Program.mkProgram (fun _ -> initModel hcf localStorage this.JSRuntime) update view
        #if DEBUG
        |> Program.withConsoleTrace
        #endif
        |> Program.withRouter router

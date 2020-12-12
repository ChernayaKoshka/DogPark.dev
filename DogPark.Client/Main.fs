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

type Page =
    | Home
    | Article of id: uint32
    | Articles
    | Login

type Submodel =
    | Home
    | Login of Login.Model
    | ArticlesList of ArticlesList.Model
    | Article of Article.Model

type SubmodelMsg =
    | LoginMsg of Login.Msg
    | ArticleMsg of Article.Msg
    | ArticlesListMsg of ArticlesList.Msg

type Message =
    | SetPage of Page

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

        Username: string option
        ErrorMessage: string option
    }

let router = Router.infer SetPage (fun m -> m.Page)

let initModel (clientFactory: IHttpClientFactory) (localStorage: ILocalStorageService) =
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

        Username = None
        ErrorMessage = None
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

let update message model =
    match message with
    | SetPage page ->
        let submodel, nextCmd =
            match page with
            | Page.Home ->
                Submodel.Home, Cmd.none
            | Page.Article id ->
                let submodel, nextCmd = Article.init model.Api id
                Submodel.Article submodel, Cmd.map ArticleMsg nextCmd
            | Page.Articles ->
                let submodel, nextCmd = ArticlesList.init model.Api
                Submodel.ArticlesList submodel, Cmd.map ArticlesListMsg nextCmd
            | Page.Login ->
                let submodel, nextCmd = Login.init model.Api
                Submodel.Login submodel, Cmd.map LoginMsg nextCmd
        { model with
            Page = page
            Submodel = submodel
        }, Cmd.map SubmodelMsg nextCmd

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
            printfn "error logging in"
            { model with Username = None }, Cmd.none
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
                do! model.LocalStorage.RemoveItemAsync("JWT")
                return! model.Api.Logout()
            })
            ()
            LogoutResult
            setError
    | LogoutResult result ->
        if result.Success then
            { model with Username = None }, Cmd.none
        else
            model, Cmd.ofMsg (setError result)

    | SetError err ->
        { model with
            ErrorMessage = err
        }, Cmd.none

    | DoNothing ->
        model, Cmd.none

    | SubmodelMsg msg ->
        let mapMsg msg =
            Cmd.map msg >> Cmd.map SubmodelMsg
        let next, nextCmd =
            match msg, model.Submodel with
            | ArticleMsg msg, Submodel.Article articleModel ->
                let next, nextCmd = Article.update articleModel msg
                Submodel.Article next, mapMsg ArticleMsg nextCmd
            | ArticlesListMsg msg, Submodel.ArticlesList articlesListModel ->
                let next, nextCmd = ArticlesList.update articlesListModel msg
                Submodel.ArticlesList next, mapMsg ArticlesListMsg nextCmd
            | LoginMsg (Login.LoginResult result), Submodel.Login loginModel ->
                Submodel.Login loginModel, Cmd.ofMsg (LoginResult result)
            | LoginMsg msg, Submodel.Login loginModel ->
                let next, nextCmd = Login.update loginModel msg
                Submodel.Login next, mapMsg LoginMsg nextCmd
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

let view model dispatch =
    let baseView =
        View()
            .LoginoutButtonLink(
                if model.Username.IsSome then
                    null
                else
                    router.Link Page.Login
            )
            .LoginoutButtonText(
                if model.Username.IsSome then
                    "Logout"
                else
                    "Login"
            )
            .LoginoutButtonClicked((fun _ ->
                if model.Username.IsSome then
                    dispatch Logout
                )
            )
            .HelloText(
                match model.Username with
                | Some username ->
                    p [ attr.``class`` "navbar-item is-size-5" ] [
                        text $"Hello, {username}"
                    ]
                | None -> Empty
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
    | page, model ->
        failwithf "'%A' and '%A' are not compatible or are not handled." page model

type MyApp() =
    inherit ProgramComponent<Model, Message>()

    override this.Program =
        let hcf = this.Services.GetService<IHttpClientFactory>()
        let localStorage = this.Services.GetService<ILocalStorageService>()
        Program.mkProgram (fun _ -> initModel hcf localStorage) update view
        #if DEBUG
        |> Program.withConsoleTrace
        #endif
        |> Program.withRouter router

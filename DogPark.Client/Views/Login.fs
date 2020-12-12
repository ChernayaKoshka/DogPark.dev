[<RequireQualifiedAccess>]
module DogPark.Client.Views.Login

open Bolero
open Bolero.Html
open DogPark.Client
open DogPark.Shared
open Elmish

type Msg =
    | SetUsername of string
    | SetPassword of string
    | Login
    | LoginResult of LoginResponse
    | SetError of string option

type LoginView = Template<"./wwwroot/templates/login.html">

type Model =
    {
        Api: Api
        Login: LoginModel
        ErrorMessage: string option
    }

let setError (o: #obj) =
    o
    |> string
    |> Some
    |> SetError

let init api =
    {
        Api = api
        Login = LoginModel.Default
        ErrorMessage = None
    }, Cmd.none

let update model message =
    match message with
    | Login ->
        if model.Login.IsValid() then
            model,
            Cmd.OfTask.either
                model.Api.Login
                model.Login
                LoginResult
                setError
        else
            model, Cmd.ofMsg (setError "Something something, don't be a moron")
    | SetError err ->
        { model with
            ErrorMessage = err
        }, Cmd.none
    | SetUsername username ->
        { model with
            Login =
                { model.Login with
                    Username = username
                }
        }, Cmd.none
    | SetPassword password ->
        { model with
            Login =
                { model.Login with
                    Password = password
                }
        }, Cmd.none
    | LoginResult _ ->
        // should be captured by the parent update function
        model, Cmd.none

let view (baseView: View) model dispatch =
    // can't have this as a template because we need to dynamically add/remove the disabled attribute.
    // which templates won't let you do... for some reason
    let loginButton =
        button [
            attr.``type`` "submit"
            attr.``class`` "button is-primary is-pulled-right"
            on.click (fun _ -> dispatch Login)
            if model.Login.IsValid() |> not then attr.disabled ""
        ] [
            text "Login"
        ]

    baseView
        .Head(Empty)
        .Content(
            LoginView()
                // This lint is nothing but lies! Kidding, but composition won't actually work here.
                // fsharplint:disable CanBeReplacedWithComposition
                .Username(model.Login.Username, (fun s -> dispatch (SetUsername s)))
                .Password(model.Login.Password, (fun s -> dispatch (SetPassword s)))
                .Keyup(fun kea -> if kea.Key = "Enter" && model.Login.IsValid() then dispatch Login)
                .LoginButton(loginButton)
                .Elt()
        )
        .Scripts(Empty)
        .Elt()
[<RequireQualifiedAccess>]
module DogPark.Client.Views.Smorpa

open Bolero
open Bolero.Html
open DogPark.Client
open DogPark.Shared
open Elmish
open Microsoft.JSInterop
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open System

type Model =
    {
        Api: Api
        CurrentSmorpa: SmorpaData option
        CurrentSmorpaIndex: int
        PassCount: int
        SmashCount: int
    }

type Msg =
    | GetNextSmorpa
    | GetSmorpa of int
    | GotSmorpa of SmorpaData
    | Smash
    | Pass
    | NewImage
    | SetError of string option

let setError (o: #obj) =
    o
    |> string
    |> Some
    |> SetError

let init api =
    {
        Api = api
        CurrentSmorpa = None
        CurrentSmorpaIndex = 0
        PassCount = 0
        SmashCount = 0
    }, Cmd.ofMsg (GetSmorpa 0)

let makeGetSmorpa api id = Cmd.OfTask.either api.GetSmorpa id GotSmorpa setError

let random = new Random()
let update (model: Model) message =
    match message with
    | GetNextSmorpa ->
        match model.CurrentSmorpa with
        | Some smorpaData ->
            model, Cmd.ofMsg (GetSmorpa (smorpaData.Id + 1))
        | None ->
            model, Cmd.ofMsg (GetSmorpa 1)
    | Smash ->
        { model with SmashCount = model.SmashCount + 1 }, Cmd.ofMsg GetNextSmorpa
    | Pass ->
        { model with PassCount = model.PassCount + 1 }, Cmd.ofMsg GetNextSmorpa
    | GetSmorpa id ->
        model, makeGetSmorpa model.Api {| Id = id |}
    | GotSmorpa smorpaData ->
        { model with 
            CurrentSmorpa = Some smorpaData 
            CurrentSmorpaIndex = random.Next(0, smorpaData.ImageUrls.Length - 1)
        }, Cmd.none
    | NewImage ->
        match model.CurrentSmorpa with
        | Some smorpaData when smorpaData.ImageUrls.Length = 1 ->
            model, Cmd.none
        | Some smorpaData ->
            let newImageUrls = Array.removeAt model.CurrentSmorpaIndex smorpaData.ImageUrls

            { model with
                CurrentSmorpa = Some ({ smorpaData with ImageUrls = newImageUrls })
                CurrentSmorpaIndex = random.Next(0, (newImageUrls.Length - 1))
            }, Cmd.none
        | None ->
            model, Cmd.none
    | SetError _ ->
        // should be consumed in main
        model, Cmd.none

type SmorpaCard = Template<"wwwroot/templates/smorpa_card.html">

let makeSmorpaCard dispatch model smorpaData =
    SmorpaCard()
        .PassCount(string model.PassCount)
        .SmashCount(string model.SmashCount)
        .Name(smorpaData.Name)
        .PokemonImageSource(smorpaData.ImageUrls[model.CurrentSmorpaIndex])
        .Smash(fun _ -> dispatch Smash)
        // I am upset I had to do this instead of using the template.
        .NewImageButton(button {
            attr.``class`` "m-1 card-footer-item button is-warning"

            attr.disabled (smorpaData.ImageUrls.Length <= 1)

            on.click (fun _ -> dispatch NewImage)

            "New Image"
        })
        .Pass(fun _ -> dispatch Pass)
        .Elt()

let view (baseView: View) (model: Model) dispatch =
    baseView
        .Head(empty())
        .Content(
            cond model.CurrentSmorpa <| function
            | Some smorpaData -> makeSmorpaCard dispatch model smorpaData
            | None -> text "Loading Smorpa"
        )
        .Scripts(empty())
        .Elt()

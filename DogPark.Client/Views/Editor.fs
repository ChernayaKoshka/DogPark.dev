[<RequireQualifiedAccess>]
module DogPark.Client.Views.Editor

open Bolero
open Bolero.Html
open DogPark.Client
open DogPark.Shared
open Elmish
open Markdig

type Model =
    {
        Title: string
        Content: string
    }

type Msg =
    | SetTitle of string
    | SetContent of string

let init() =
    {
        Title = ""
        Content = ""
    }, Cmd.none

let update model message =
    match message with
    | SetTitle title ->
        { model with
            Title = title
        }, Cmd.none
    | SetContent content ->
        { model with
            Content = content
        }, Cmd.none

type EditorView = Template<"./wwwroot/templates/editor.html">
let view (baseView: View) model dispatch =
    baseView
        .Head(Empty)
        .Content(
            EditorView()
                // fsharplint:disable CanBeReplacedWithComposition
                .Title(model.Title, fun title -> dispatch (SetTitle title))
                .Content(model.Content, fun content -> dispatch (SetContent content))
                .Preview(
                    RawHtml (Markdig.Markdown.ToHtml(model.Content, markdownPipeline))
                )
                .Elt()
        )
        .Scripts(Empty)
        .Elt()

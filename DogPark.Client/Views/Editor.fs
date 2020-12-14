[<RequireQualifiedAccess>]
module DogPark.Client.Views.Editor

open Bolero
open Bolero.Html
open DogPark.Client
open DogPark.Shared
open Elmish
open Markdig
open Microsoft.JSInterop
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open System

type Model =
    {
        JSRuntime: IJSRuntime
        Title: string
        Content: string
        HighlightPending: bool
    }

type Msg =
    | SetTitle of string
    | SetContent of string
    | BeginHighlight
    | EndHighlight

let init jsRuntime =
    {
        JSRuntime = jsRuntime
        Title = ""
        Content = ""
        HighlightPending = false
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
        },
        if not model.HighlightPending then Cmd.ofMsg BeginHighlight
        else Cmd.none
    | BeginHighlight ->
        if not model.HighlightPending then
            { model with
                HighlightPending = true
            },
            Cmd.OfTask.perform
                (fun () -> task {
                    do! Task.Delay(TimeSpan.FromSeconds(0.5))
                    do! model.JSRuntime.InvokeVoidAsync("highlight")
                    return EndHighlight
                })
                ()
                id
        else
            model, Cmd.none
    | EndHighlight ->
        { model with
            HighlightPending = false
        }, Cmd.none

type EditorView = Template<"./wwwroot/templates/editor.html">
let view (baseView: View) model dispatch =
    baseView
        .Head(link [ attr.rel "stylesheet"; attr.href "css/vs.css" ])
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
        .Scripts(
            concat [
                script [ attr.src "scripts/highlight.pack.js" ] [ ]
                script [ ] [
                    text
                        """
                        window.highlight = () => {
                            var blocks = document.querySelectorAll('pre code:not(.hljs)');
                            Array.prototype.forEach.call(blocks, hljs.highlightBlock);
                        }
                        """
                ]
            ])
        .Elt()

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

type HighlightState =
    | Available
    | Pending
    | Highlighting

type Model =
    {
        JSRuntime: IJSRuntime
        Title: string
        Content: string
        LastContentUpdate: DateTime ref
        HighlightState: HighlightState
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
        LastContentUpdate = ref DateTime.Now
        HighlightState = Available
    }, Cmd.none

let update model message =
    match message with
    | SetTitle title ->
        { model with
            Title = title
        }, Cmd.none
    | SetContent content ->
        model.LastContentUpdate := DateTime.Now
        { model with
            Content = content
            HighlightState = if model.HighlightState = Available then Pending else model.HighlightState
        },
        if model.HighlightState = Available then
            Cmd.OfTask.perform
                (fun () -> task {
                    while DateTime.Now - !model.LastContentUpdate <= TimeSpan.FromSeconds(0.5) do
                        do! Task.Delay(TimeSpan.FromSeconds(0.25))
                    return BeginHighlight
                })
                ()
                id
        else
            Cmd.none
    | BeginHighlight ->
        if model.HighlightState = Pending then
            { model with
                HighlightState = Highlighting
            },
            Cmd.OfTask.perform
                (fun () -> task {
                    do! model.JSRuntime.InvokeVoidAsync("highlight")
                    return EndHighlight
                })
                ()
                id
        else
            model, Cmd.none
    | EndHighlight ->
        { model with
            HighlightState = Available
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

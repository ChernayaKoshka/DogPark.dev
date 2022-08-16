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
        Api: Api
        JSRuntime: IJSRuntime
        Article: PostArticle
        LastContentUpdate: DateTime ref
        HighlightState: HighlightState
    }

type Msg =
    | SetTitle of string
    | SetContent of string
    | BeginHighlight
    | EndHighlight
    | Submit
    | SubmitResult of PostArticleResponse
    | SetError of string option

let setError (o: #obj) =
    o
    |> string
    |> Some
    |> SetError

let init api jsRuntime =
    {
        Api = api
        JSRuntime = jsRuntime
        Article = { Headline = String.Empty; Content = String.Empty }
        LastContentUpdate = ref DateTime.Now
        HighlightState = Available
    }, Cmd.none

let update (model: Model) message =
    match message with
    | SetTitle title ->
        { model with
            Article =
                { model.Article with
                    Headline = title
                }
        }, Cmd.none
    | SetContent content ->
        model.LastContentUpdate.Value <- DateTime.Now
        { model with
            Article =
                { model.Article with
                    Content = content
                }
            HighlightState = if model.HighlightState = Available then Pending else model.HighlightState
        },
        if model.HighlightState = Available then
            Cmd.OfTask.perform
                (fun () -> task {
                    while DateTime.Now - model.LastContentUpdate.Value <= TimeSpan.FromSeconds(0.5) do
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
    | SetError _ ->
        // should be consumed in main
        model, Cmd.none
    | Submit ->
        model,
        Cmd.OfTask.either
            (fun article -> task {
                return! model.Api.PostArticle article
            })
            model.Article
            SubmitResult
            setError
    | SubmitResult _ ->
        // should be consumed in main
        model, Cmd.none

type EditorView = Template<"./wwwroot/templates/editor.html">
let view (baseView: View) (model: Model) dispatch =
    let errors = model.Article.HasErrors()
    baseView
        .Head(link { attr.rel "stylesheet"; attr.href "css/vs.css" })
        .Content(
            EditorView()
                // fsharplint:disable CanBeReplacedWithComposition
                .Title(model.Article.Headline, fun title -> dispatch (SetTitle title))
                .Content(model.Article.Content, fun content -> dispatch (SetContent content))
                .Errors(
                    match errors with
                    | Some errors ->
                        ErrorNotificationNoButton()
                            .Content(concat { forEach errors (fun t -> p { text t }) })
                            .Elt()
                    | None ->
                        empty()
                )
                .SubmitButton(
                    div { 
                        attr.``class`` "field" 
                        div { 
                            attr.``class`` "control"
                            button {
                                attr.``class`` "button is-link is-pulled-right"
                                if errors.IsSome then attr.disabled "true"
                                else on.click (fun _ -> dispatch Submit)
                                
                                text "Submit"
                            }
                        }
                    }
                )
                .Preview(
                    rawHtml (Markdig.Markdown.ToHtml(model.Article.Content, markdownPipeline))
                )
                .Elt()
        )
        .Scripts(
            concat {
                script { attr.src "scripts/highlight.pack.js" }
                script {
                    text
                        """
                        window.highlight = () => {
                            var blocks = document.querySelectorAll('pre code:not(.hljs)');
                            Array.prototype.forEach.call(blocks, hljs.highlightBlock);
                        }
                        """
                }
            })
        .Elt()

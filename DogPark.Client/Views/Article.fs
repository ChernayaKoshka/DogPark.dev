[<RequireQualifiedAccess>]
module DogPark.Client.Views.Article

open Bolero
open Bolero.Html
open DogPark.Client
open DogPark.Shared
open Elmish

type Model =
    {
        Api: Api
        Article: Article option
        ErrorMessage: string option
    }

type Msg =
    | GetArticle of id: uint32
    | GotArticle of Article
    | SetError of string option

let setError (o: #obj) =
    o
    |> string
    |> Some
    |> SetError

let init api id =
    {
        Api = api
        Article = None
        ErrorMessage = None
    }, Cmd.ofMsg (GetArticle id)

let update model message =
    match message with
    | GetArticle id ->
        model,
        Cmd.OfTask.either
            model.Api.Article
            {| Id = id |}
            GotArticle
            setError
    | GotArticle article ->
        { model with
            Article = Some article
        }, Cmd.none
    | SetError err ->
        { model with
            ErrorMessage = err
        }, Cmd.none

type Article = Template<"./wwwroot/templates/article.html">
let view (baseView: View) model dispatch =
    baseView
        .Head(link { attr.rel "stylesheet"; attr.href "css/vs.css" })
        .Content(
            div {
                cond model.Article <| function
                | Some article ->
                    Article()
                        .Title(article.Details.Headline)
                        .Subtitle($"Author: {article.Details.Author} @ {article.Details.Created}")
                        .Content(rawHtml article.HtmlBody)
                        .Elt()
                | None ->
                    text "Loading..."
            }
        )
        .Scripts(
            concat {
                script { attr.src "scripts/highlight.pack.js" }
                cond model.Article <| function
                | Some _ ->
                    script {
                        text
                            """
                            var blocks = document.querySelectorAll('pre code:not(.hljs)');
                            Array.prototype.forEach.call(blocks, hljs.highlightBlock);
                            """
                    }
                | None ->
                    empty()
            }
        )
        .Elt()
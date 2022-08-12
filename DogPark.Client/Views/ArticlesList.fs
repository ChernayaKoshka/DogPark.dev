[<RequireQualifiedAccess>]
module DogPark.Client.Views.ArticlesList

open Bolero
open Bolero.Html
open DogPark.Client
open DogPark.Shared
open Elmish

type Model =
    {
        Api: Api
        ArticlesList: ArticleDetails seq
        ErrorMessage: string option
    }

type Msg =
    | GetArticlesList
    | GotArticlesList of ArticleDetails seq
    | SetError of string option

let init (api: Api) =
    {
        Api = api
        ArticlesList = Seq.empty
        ErrorMessage = None
    }, Cmd.ofMsg GetArticlesList

let setError (o: #obj) =
    o
    |> string
    |> Some
    |> SetError

let update (model: Model) (message: Msg) =
    match message with
    | GetArticlesList ->
        model,
        Cmd.OfTask.either
            model.Api.Articles
            ()
            GotArticlesList
            setError
    | GotArticlesList articles ->
        { model with
            ArticlesList = articles
        }, Cmd.none
    | SetError err ->
        { model with
            ErrorMessage = Some (string err)
        }, Cmd.none

let view (baseView: View) model dispatch =
    baseView
        .Head(empty())
        .Content(
            div {
                ul {
                    forEach
                        model.ArticlesList
                        (fun al ->
                            li {
                                text $"{al.Author} @ {al.Created} - "
                                a { attr.href $"Article/{al.Id}"; text al.Headline }
                            }
                        )
                }
            }
        )
        .Scripts(empty())
        .Elt()
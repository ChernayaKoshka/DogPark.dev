[<RequireQualifiedAccess>]
module DogPark.Handlers

open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open System
open System.IO
open System.Threading.Tasks

let error (ex : Exception) (logger : ILogger) : HttpHandler =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text "Something went wrong!"

let finishEarly : HttpFunc = Some >> Task.FromResult

let notFound = RequestErrors.notFound (text String.Empty) finishEarly

let showArticle(article : Task<Article option>) : HttpHandler = 
    fun next ctx -> task {
        let! article = article
        match article with
        | Some article -> return! htmlView (Views.articleView article) next ctx
        | None ->  return! notFound ctx
    }

let showArticleById (id : int) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> task {
        let article = Api.getArticleById id
        return! showArticle article next ctx
    }

let showArticleList : HttpHandler =
    fun next ctx -> task {
        let! articles = Api.getAllDbArticles
        return!
            htmlView
                (articles
                |> Seq.map Views.articleListItem
                |> Views.articleListTable
                |> List.singleton
                |> Views.layout)
                next 
                ctx         
    }

let redirectShortUrl (short : string) : HttpHandler =
    fun next ctx -> task { 
        let! long = Api.tryFindShortUrl short
        match long with
        | Some long -> return! redirectTo true long next ctx
        | None -> return! RequestErrors.notFound (text (sprintf "Short URL '%s' does not exist on this server." short)) next ctx
    }

[<RequireQualifiedAccess>]
module Api =
    let getArticle (article : DBArticle) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) -> task {
            let path = Path.Combine(articleRoot, article.FilePath)

            if File.Exists path then
                let! article = Api.readArticle article
                return! negotiate article next ctx
            else
                return! notFound ctx
        }

    let getArticleById (id : int) : HttpHandler = 
        fun next ctx -> task {
            let! article = Api.getDbArticleById id
            match article with
            | Some article -> 
                return! getArticle article next ctx
            | None -> 
                return! notFound ctx
        }
    
    let createShortUrl : HttpHandler =
        fun next ctx -> task {
            let! long = ctx.BindFormAsync<ShortenUrlPostData>()
            match tryMakeUrl long.LongUrl with
            | Ok uri ->
                let! result = Api.createShortUrl uri.AbsoluteUri
                match result with
                | Ok short ->
                    return! htmlView (Views.urlShortenerSuccess short) next ctx
                | Error err ->
                    return! ServerErrors.internalError (text err) next ctx
            | Error err ->
                return! RequestErrors.badRequest (text (sprintf "'%s' is not a valid URL.\n%s" long.LongUrl err)) next ctx
        }
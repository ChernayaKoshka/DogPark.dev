module DogPark.Api

open Dapper
open FSharp.Control.Tasks.V2.ContextInsensitive
open Markdig
open MySql.Data.MySqlClient
open System.IO


type Api(connectionString) =
    // TODO: Use closure
    let mutable cacheMap : Map<DBArticle, Article> = Map.empty

    let makeShortUrlString() =
        let max = urlDictionary.Length - 1
        urlDictionary.[rand.Next(0, max)] + urlDictionary.[rand.Next(0, max)] + urlDictionary.[rand.Next(0, max)]

    member __.TryFindLongUrlFromShortUrl (shortUrl: string) = task {
        use con = new MySqlConnection(connectionString)
        do! con.OpenAsync()
        let! result = con.QueryFirstOrDefaultAsync<string>("SELECT `long` FROM SHORTURL WHERE `short` = @Short", {| Short = shortUrl |})
        if isNull result then
            return None
        else
            return Some result
    }

    member __.TryFindShortUrlFromLongUrl (longUrl: string) = task {
        use con = new MySqlConnection(connectionString)
        do! con.OpenAsync()
        let! result = con.QueryFirstOrDefaultAsync<string>("SELECT `short` FROM SHORTURL WHERE `long` = @Long", {| Long = longUrl |})
        if isNull result then
            return None
        else
            return Some result
    }

    member this.CreateShortUrl (longUrl : string) = task {
        use con = new MySqlConnection(connectionString)
        do! con.OpenAsync()
        let shortUrl = makeShortUrlString()

        match tryMakeUrl longUrl with
        | Ok uri ->
            try
                match! this.TryFindShortUrlFromLongUrl longUrl with
                | Some short ->
                    return Ok short
                | None ->
                    let! result = con.ExecuteAsync("INSERT INTO SHORTURL (`short`, `long`) VALUES (@Short, @Long)", {| Short = shortUrl; Long = uri.AbsoluteUri |})
                    
                    if result <> 1 then
                        return Error "Insertion failed!"
                    else
                        return Ok shortUrl
            with
            | ex ->
                return
                    ex
                    |> string
                    |> Error
        | Error err ->
            return Error err
    }

    member __.GetAllDbArticles = task {
        use con = new MySqlConnection(connectionString)
        do! con.OpenAsync()
        let! result = con.QueryAsync<DBArticle>("SELECT * FROM ARTICLE")
        return seq result
    }

    member __.GetDbArticleById (article : int) = task {
        use con = new MySqlConnection(connectionString)
        do! con.OpenAsync()
        let! result = con.QueryFirstOrDefaultAsync<DBArticle>("SELECT * FROM ARTICLE WHERE ARTICLE = @Article", {| Article = article |})
        return
            if result = Unchecked.defaultof<DBArticle> then
                None
            else
                Some result
    }

    member __.ReadArticle (article : DBArticle) = task {
        match Map.tryFind article cacheMap with
        | Some cached ->
            printfn "Cache hit! %A -> %A" article cached
            return cached
        | None ->
            printfn "Cache miss: %A" article
            let! body = File.ReadAllTextAsync(Path.Combine(articleRoot, article.FilePath))
            let article' = { Author = article.Author; Headline = article.Headline; Body = Markdown.ToHtml(body, markdownPipeline) }
            cacheMap <- Map.add article article' cacheMap
            return article'
    }

    member this.GetArticleById (article : int) = task {
        match! this.GetDbArticleById article with
        | Some dbArticle -> 
            let! read = this.ReadArticle dbArticle
            return Some read
        | None -> 
            return None
    }
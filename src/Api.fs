[<RequireQualifiedAccess>]
module DogPark.Api

open Dapper
open FSharp.Control.Tasks.V2.ContextInsensitive
open Markdig
open MySql.Data.MySqlClient
open System.IO

let makeShortUrlString() =
    let max = urlDictionary.Length - 1
    urlDictionary.[rand.Next(0, max)] + urlDictionary.[rand.Next(0, max)] + urlDictionary.[rand.Next(0, max)]

let createShortUrl (longUrl : string) = task {
    use con = new MySqlConnection(MDBConnectionString)
    do! con.OpenAsync()
    let shortUrl = makeShortUrlString()
    try
        let! result = con.ExecuteAsync("INSERT INTO SHORTURL (`short`, `long`) VALUES (@Short, @Long)", {| Short = shortUrl; Long = longUrl |})
        
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
}

let tryFindShortUrl (shortUrl : string) = task {
    use con = new MySqlConnection(MDBConnectionString)
    do! con.OpenAsync()
    let! result = con.QueryFirstOrDefaultAsync<string>("SELECT `long` FROM SHORTURL WHERE `short` = @Short", {| Short = shortUrl |})
    if isNull result then
        return None
    else
        return Some result
}

let getAllDbArticles = task {
    use con = new MySqlConnection(MDBConnectionString)
    do! con.OpenAsync()
    let! result = con.QueryAsync<DBArticle>("SELECT * FROM ARTICLE")
    return seq result
}

let getDbArticleById (article : int) = task {
    use con = new MySqlConnection(MDBConnectionString)
    do! con.OpenAsync()
    let! result = con.QueryFirstOrDefaultAsync<DBArticle>("SELECT * FROM ARTICLE WHERE ARTICLE = @Article", {| Article = article |})
    return
        if result = Unchecked.defaultof<DBArticle> then
            None
        else
            Some result
}

// TODO: Use closure
let mutable private cacheMap : Map<DBArticle, Article> = Map.empty

let readArticle (article : DBArticle) = task {
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

let getArticleById (article : int) = task {
    match! getDbArticleById article with
    | Some dbArticle -> 
        let! read = readArticle dbArticle
        return Some read
    | None -> 
        return None
}
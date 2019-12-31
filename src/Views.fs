module DogPark.Views
open Giraffe.GiraffeViewEngine

let layout (content: XmlNode list) =
    html [] [
        head [] [
            title []  [ encodedText "DogPark" ]
            link [ _rel  "stylesheet"
                   _type "text/css"
                   _href "/main.css" ]
        ]
        nav [ ] [ 
            a [ _href "/home" ] [ Text "Home" ]
            a [ _href "/articles" ] [ Text "Articles" ]
            a [ _href "/about" ] [ Text "About" ]
         ]
        body [] content
    ]

let about =
    [
        a [ ] [
            Text "A developer who loves dogs"
        ]
    ]
    |> layout

let partial () =
    h1 [] [ encodedText "DogPark" ]

let articleView (article : Article) =
    [
        link [ 
                _rel "stylesheet" 
                _href "//cdn.jsdelivr.net/gh/highlightjs/cdn-release@9.17.1/build/styles/default.min.css"
            ]
        script [ _src "//cdn.jsdelivr.net/gh/highlightjs/cdn-release@9.17.1/build/highlight.min.js" ] [ ]
        script [ _src "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/9.15.9/languages/fsharp.min.js" ] [ ]
        script [ ] [ rawText "hljs.initHighlightingOnLoad();" ]
        partial()
        h2 [ ] [ str article.Headline ]
        h3 [ ] [ str article.Author ]
        p  [ ] [ rawText article.Body ]
    ] |> layout

let articleListTable articleListItems =
    table [ ] [
        yield
            tr [ ] [ 
                th [ ] [ Text "Headline" ]
                th [ ] [ Text "Author" ]
            ]
        yield! articleListItems
    ]

let articleListItem (article : DBArticle) =
    [
        tr [ ] [ 
            td [ ] [ 
                a [ _href (sprintf "/article/%d" article.Article) ] [ 
                    Text article.Headline
                ]
            ]
            td [ ] [ Text article.Author ]
        ]
    ]
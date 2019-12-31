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
        body [] content
    ]

let partial () =
    h1 [] [ encodedText "DogPark" ]

let articleView (article : Article) =
    [
        partial()
        h2 [ ] [ str article.Headline ]
        h3 [ ] [ str article.Author ]
        p  [ ] [ rawText article.Body ]
    ] |> layout
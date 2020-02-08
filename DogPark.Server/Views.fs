module DogPark.Views
open Giraffe.GiraffeViewEngine
open DogPark.Authentication

let layout (isSignedIn: bool) (content: XmlNode list) =
    html [] [
        head [] [
            title []  [ encodedText "DogPark" ]
            link [ _rel  "stylesheet"
                   _type "text/css"
                   _href "/main.css" ]
        ]
        nav [ ] [ 
            yield a [ _href "/home" ] [ Text "Home" ]
            yield a [ _href "/articles" ] [ Text "Articles" ]
            yield a [ _href "/shorten" ] [ Text "URL Shortener" ]
            yield a [ _href "/about" ] [ Text "About" ]
            if isSignedIn then 
                yield a [ _href "/logout" ] [ Text "logout" ]
            else 
                yield! [ 
                    a [ _href "/login" ] [ Text "login" ] 
                    a [ _href "/register" ] [ Text "Register" ] 
                ]
         ]
        body [] content
    ]

let registerPage =
    [
        form [ _action "/register"; _method "POST" ] [
            div [] [
                label [] [ str "User name:" ]
                input [ _name "UserName"; _type "text" ]
            ]
            div [] [
                label [] [ str "Password:" ]
                input [ _name "Password"; _type "password" ]
            ]
            input [ _type "submit" ]
        ]
    ] |> layout false

let loginPage (loginFailed : bool) =
    [
        if loginFailed then yield p [ _style "color: Red;" ] [ str "Login failed." ]

        yield form [ _action "/login"; _method "POST" ] [
            div [] [
                label [] [ str "User name:" ]
                input [ _name "UserName"; _type "text" ]
            ]
            div [] [
                label [] [ str "Password:" ]
                input [ _name "Password"; _type "password" ]
            ]
            input [ _type "submit" ]
        ]
    ] |> layout false

let userPage (user : User) =
    [
        p [] [
            sprintf "User name: %s" user.UserName
            |> str
        ]
    ] |> layout true

let about isSignedIn =
    [
        a [ ] [
            Text "A developer who loves dogs"
        ]
    ]
    |> layout isSignedIn

let partial () =
    h1 [] [ encodedText "DogPark" ]

let includeHighlightJs() =
    [
        link [ 
            _rel "stylesheet" 
            _href "//cdn.jsdelivr.net/gh/highlightjs/cdn-release@9.17.1/build/styles/default.min.css"
        ]
        script [ _src "//cdn.jsdelivr.net/gh/highlightjs/cdn-release@9.17.1/build/highlight.min.js" ] [ ]
        script [ _src "https://cdnjs.cloudflare.com/ajax/libs/highlight.js/9.15.9/languages/fsharp.min.js" ] [ ]
        script [ ] [ rawText "hljs.initHighlightingOnLoad();" ]
    ]


let articleView isSignedIn (article : Article) =
    [
        yield! includeHighlightJs()
        partial()
        h2 [ ] [ str article.Headline ]
        h3 [ ] [ str article.Author ]
        p  [ ] [ rawText article.Body ]
    ] |> layout isSignedIn

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
    tr [ ] [ 
        td [ ] [ 
            a [ _href (sprintf "/article/%d" article.Article) ] [ 
                Text article.Headline
            ]
        ]
        td [ ] [ Text article.Author ]
    ]

let urlShortenerForm =
    div [ ] [
        script [ _src "./scripts/Client.js" ] [ ]
        form [ _name "shortenerForm"; _action "/shorten"; _method "POST"; attr "onsubmit" "return validateShortenerForm()"  ] [
            div [ ] [
                label [ ] [ str "Long Url: " ]
                input [ _type "text"; _id "LongUrl"; _name "LongUrl" ]
            ]
            input [ _type "Submit" ]
        ]
        div [ _id "shorteningErrors"; ] [ ]
    ]

let urlShortenerSuccess isSignedIn short =
    let url = sprintf "https://dogpark.dev/@%s" short
    [
        div [ ] [
            label [ ] [ str "Short Url:" ]
            a [ _href url ] [ str url ]
        ]
    ]
    |> layout isSignedIn
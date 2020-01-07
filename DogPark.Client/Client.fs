module DogPark.Client.JS

open Fable.Core
open Fable.Core.JsInterop
open Browser.Types
open Browser

let validateUrl (url : string) =
    try
        let url =
            if not <| url.StartsWith("http") then
                "http://" + url
            else 
                url
        let url = URL.Create url
        url.protocol.StartsWith("http")
    with
    | _ ->
        false

let removeChildren (element : HTMLElement) =
    while (isNull >> not) element.firstChild do
        element.firstChild
        |> element.removeChild
        |> ignore

let validateShortenerForm() =
    let errorNode : HTMLDivElement = unbox document.getElementById "shorteningErrors"
    removeChildren errorNode

    let url = (document.getElementById "LongUrl").nodeValue

    if not <| validateUrl url then
        errorNode?style?display <- "block"
        let error : Types.HTMLParagraphElement = unbox document.createElement "p"
        error.textContent <- "URL is not valid!"
        errorNode.appendChild error
        |> ignore
        false
    else
        true
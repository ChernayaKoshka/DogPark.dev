[<AutoOpen>]
module DogPark.Common

open System.IO
open System
open Markdig
open System.Text.RegularExpressions
open FSharp.Control.Tasks.V2.ContextInsensitive
open MySql.Data.MySqlClient
open System.Threading.Tasks

#if DEBUG
// !@#$ing stupid that I have to do this
Directory.SetCurrentDirectory("bin/Debug/netcoreapp3.1/")
#endif

let contentRoot = AppContext.BaseDirectory
let logRoot     = Path.Combine(contentRoot, "Logs")
let webRoot     = Path.Combine(contentRoot, "WebRoot")
let articleRoot = Path.Combine(contentRoot, "DogPark-Articles")

let markdownPipeline = MarkdownPipelineBuilder().DisableHtml().Build()

let rand = Random()

let urlDictionary =
    Path.Combine(contentRoot, "urldictionary.txt")
    |> File.ReadAllLines

// http://data.iana.org/TLD/tlds-alpha-by-domain.txt
let topLevelDomains =
    Path.Combine(contentRoot, "tlds-alpha-by-domain.txt")
    |> File.ReadAllLines
    |> Array.filter (fun str -> str.StartsWith("#") |> not || String.IsNullOrWhiteSpace(str))

let private tldsRegex =
    topLevelDomains
    |> String.concat "|"
    |> sprintf @"\.(?:%s)"

let tryMakeUrl (url : string) =
    let url =
        if not <| Regex.IsMatch(url, "^https?://") then
            "http://" + url
        else
            url
    if Uri.IsWellFormedUriString(url, UriKind.Absolute) then
        match Uri.TryCreate(url, UriKind.Absolute) with
        | (true, uri) ->
            if Regex.IsMatch(uri.Host, tldsRegex + "$", RegexOptions.IgnoreCase) then
                Ok uri
            else
                Error "Invalid or missing domain!"
        | _ ->
            Error "URL is not well-formed!"
    else
        Error "URL is not well-formed!"

// let tests =
//     [
//         @"https://www.google.com/w o w"
//         @"https://www.google.com"
//         @"http://www.google.com"
//         @"www.google.com"
//         @"google.com"
//         @"javascript:alert('Hack me!')"
//         @"http://en.wikipedia.org/wiki/Procter_&_Gamble"
//         @"http://www.google.com/url?sa=i&rct=j&q=&esrc=s&source=images&cd=&docid=nIv5rk2GyP3hXM&tbnid=isiOkMe3nCtexM:&ved=0CAUQjRw&url=http%3A%2F%2Fanimalcrossing.wikia.com%2Fwiki%2FLion&ei=ygZXU_2fGKbMsQTf4YLgAQ&bvm=bv.65177938,d.aWc&psig=AFQjCNEpBfKnal9kU7Zu4n7RnEt2nerN4g&ust=1398298682009707"
//         @"https://sdfasd"
//         @"dfdsfdsfdfdsfsdfs"
//         @"magnet:?xt=urn:btih:123"
//         @"https://stackoverflow.com/"
//         @"https://w"
//         @"https://sdfasdp.ppppppppppp"
//     ]
//
// tests
// |> List.choose isValidUrl
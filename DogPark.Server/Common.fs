[<AutoOpen>]
module DogPark.Common

open System.IO
open System
open Markdig
open System.Security.Cryptography

let contentRoot     =
    #if DEBUG
    Path.Combine(__SOURCE_DIRECTORY__, "../DogPark.Client/")
    #else
    AppContext.BaseDirectory
    #endif
let logRoot         = Path.Combine(contentRoot, "logs")
let webRoot         = Path.Combine(contentRoot, "wwwroot")
let blazorFramework = Path.Combine(webRoot, "_framework")
let articleRoot     = Path.Combine(webRoot, "articles")

let markdownPipeline = MarkdownPipelineBuilder().DisableHtml().Build()
let rand = Random()

let generateKeypair() =
    use rsa = RSA.Create(2048)
    let publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey())
    let privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey())
    publicKey, privateKey
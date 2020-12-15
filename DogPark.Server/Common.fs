[<AutoOpen>]
module DogPark.Common

open System.IO
open System
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

let rand = Random()

let sanitizeFilename (name: string) =
    name
    |> Seq.map (fun c ->
        if Array.contains c (Path.GetInvalidFileNameChars()) then '-'
        else c
    )
    |> Array.ofSeq
    |> String

let generateKeypair() =
    use rsa = RSA.Create(2048)
    let publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey())
    let privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey())
    publicKey, privateKey
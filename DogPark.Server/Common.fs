[<AutoOpen>]
module DogPark.Common

open System.IO
open System
open Markdig

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
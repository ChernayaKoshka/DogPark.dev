[<AutoOpen>]
module DogPark.Common

open System.IO
open Markdig

let contentRoot = Directory.GetCurrentDirectory()
let webRoot     = Path.Combine(contentRoot, "WebRoot")
let articleRoot = Path.Combine(contentRoot, "DogPark-Articles")

let markdownPipeline = MarkdownPipelineBuilder().DisableHtml().Build()

let [<Literal>] mdbConnectionString = """Server=localhost;Uid=DogPark;Database=DogPark;Port=3306"""

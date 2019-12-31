[<AutoOpen>]
module DogPark.Common

open System.IO
open Markdig

#if DEBUG
Directory.SetCurrentDirectory("bin/Debug/netcoreapp3.1/")
#endif

let contentRoot = Directory.GetCurrentDirectory()
let webRoot     = Path.Combine(contentRoot, "WebRoot")
let articleRoot = Path.Combine(contentRoot, "DogPark-Articles")

let markdownPipeline = MarkdownPipelineBuilder().DisableHtml().Build()

let [<Literal>] MDBConnectionString = """Server=localhost;Uid=DogPark;Database=DogPark;Port=3306"""

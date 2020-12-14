[<AutoOpen>]
module DogPark.Shared.Shared

open System.Text.Json
open System.Text.Json.Serialization
open Markdig

let jsonOptions = JsonSerializerOptions()
jsonOptions.Converters.Add(JsonFSharpConverter())

let markdownPipeline = MarkdownPipelineBuilder().DisableHtml().Build()

let jwtDecodeNoVerify jwt =
    let handler = System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
    handler.ReadJwtToken(jwt)
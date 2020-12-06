[<AutoOpen>]
module DogPark.Shared.Shared

open System.Text.Json
open System.Text.Json.Serialization

let jsonOptions = JsonSerializerOptions()
jsonOptions.Converters.Add(JsonFSharpConverter())
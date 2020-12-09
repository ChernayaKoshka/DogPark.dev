[<AutoOpen>]
module DogPark.Shared.Shared

open System.Text.Json
open System.Text.Json.Serialization

let jsonOptions = JsonSerializerOptions()
jsonOptions.Converters.Add(JsonFSharpConverter())

let jwtDecodeNoVerify jwt =
    let handler = System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
    handler.ReadJwtToken(jwt)
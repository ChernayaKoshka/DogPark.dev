[<AutoOpen>]
module DogPark.Types
open System

[<AllowNullLiteral>]
type User() =
    member val IDUser = 0u with get, set
    member val UserName : string = null with get, set
    member val NormalizedUserName : string = null with get, set
    member val PasswordHash : string = null with get, set

[<AllowNullLiteral>]
type Role() =
    member val IDRole = 0u with get, set
    member val Name = "ERROR" with get, set
    member val NormalizedName = "ERROR" with get, set

type ArticleDto =
    {
        IDArticle: uint32
        UserName: string
        Created: DateTime
        Modified: DateTime
        Headline: string
        FilePath: string
    }

type ShortUrlDto =
    {
        IDShortUrl: int
        IDUser: int
        Short: string
        Long: string
    }
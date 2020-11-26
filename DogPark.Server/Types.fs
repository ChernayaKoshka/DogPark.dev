[<AutoOpen>]
module DogPark.Types

type User() =
    member val IDUser = -1 with get, set
    member val UserName : string = null with get, set
    member val NormalizedUserName : string = null with get, set
    member val PasswordHash : string = null with get, set

type Role() =
    member val IDRole = -1 with get, set
    member val Name = "ERROR" with get, set
    member val NormalizedName = "ERROR" with get, set

type Article =
    {
        IDArticle: int
        IDUser: int
        Headline: string
        FilePath: string
    }

type ShortUrl =
    {
        IDShortUrl: int
        IDUser: int
        Short: string
        Long: string
    }
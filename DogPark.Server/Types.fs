[<AutoOpen>]
module DogPark.Types
open System

[<AllowNullLiteral>]
type User() =
    member val IDUser = -1 with get, set
    member val UserName : string = null with get, set
    member val NormalizedUserName : string = null with get, set
    member val PasswordHash : string = null with get, set

[<AllowNullLiteral>]
type Role() =
    member val IDRole = -1 with get, set
    member val Name = "ERROR" with get, set
    member val NormalizedName = "ERROR" with get, set


type ChangePasswordModel =
    {
        OldPassword: string
        NewPassword: string
    }

type LoginModel =
    {
        Username: string
        Password: string
    }

type ArticleDto =
    {
        UserName: string
        Created: DateTime
        Modified: DateTime
        Headline: string
        FilePath: string
    }

type ArticleDetails =
    {
        Author: string
        Created: DateTime
        Modified: DateTime
        Headline: string
    }

type Article =
    {
        Details: ArticleDetails
        Body: string
        HtmlBody: string
    }

type ShortUrl =
    {
        IDShortUrl: int
        IDUser: int
        Short: string
        Long: string
    }
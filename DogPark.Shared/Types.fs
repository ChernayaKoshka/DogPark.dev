
[<AutoOpen>]
module DogPark.Shared.Types

open System

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
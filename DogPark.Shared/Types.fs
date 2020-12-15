
[<AutoOpen>]
module DogPark.Shared.Types

open System
open System.Security.Claims

type ChangePasswordModel =
    {
        OldPassword: string
        NewPassword: string
    }
    with
        static member Default =
            { OldPassword = String.Empty; NewPassword = String.Empty }

type LoginModel =
    {
        Username: string
        Password: string
    }
    with
        static member Default =
            { Username = String.Empty; Password = String.Empty }
        member this.IsValid() =
            let isFieldVald s = not (String.IsNullOrWhiteSpace(s) || s.Contains(" "))
            isFieldVald this.Username && isFieldVald this.Password

type ArticleDetails =
    {
        Id: uint32
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

type GenericResponse =
    {
        Success: bool
        Message: string
    }

type RefreshToken =
    {
        Username: string
        TokenString: string
        ExpireAt: DateTime
    }

type JwtAuthResult =
    {
        AccessToken: string
        RefreshToken: RefreshToken
    }

type AccountDetails =
    {
        Username: string
    }

type AccountDetailsResponse =
    {
        Success: bool
        Details: AccountDetails option
        Message: string option
    }

type LoginDetails =
    {
        Username: string
        Jwt: JwtAuthResult
    }

type LoginResponse =
    {
        Success: bool
        Details: LoginDetails option
        Message: string option
    }

[<CLIMutable>]
type PostArticle =
    {
        Headline: string
        Content: string
    }
    with
        member this.HasErrors() =
            let errors =
                [
                    if String.IsNullOrWhiteSpace(this.Headline) then
                        "Headline must not be null or whitespace."
                    else
                        if this.Headline.Length > 255 then "Headline cannot be longer than 50 characters"
                        if this.Headline.Length < 10 then "Headline must be at least 10 characters"

                    if String.IsNullOrWhiteSpace(this.Content) then
                        "Content must not be null or whitespace."
                    else
                        if this.Content.Length > 16000 then "Content must be fewer than 16000 bytes"
                        if this.Content.Length < 256 then "Content must be greater than 256 bytes"
                ]
            if errors.Length <> 0 then
                Some errors
            else
                None


type PostArticleResponse =
    {
        Success: bool
        Id: uint32
        Message: string option
    }
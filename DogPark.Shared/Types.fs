﻿
[<AutoOpen>]
module DogPark.Shared.Types

open System

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
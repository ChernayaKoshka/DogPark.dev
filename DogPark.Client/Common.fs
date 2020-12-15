[<AutoOpen>]
module DogPark.Client.Common

open Bolero
open DogPark.Shared
open EasyHttp
open System
open System.Threading.Tasks

[<CustomEquality; CustomComparison>]
type Api =
    {
        [<Method("GET")>]
        [<Path("ping")>]
        Ping: unit -> Task<string>

        [<Method("GET")>]
        [<Path("article")>]
        Articles: unit -> Task<ArticleDetails seq>

        [<Method("GET")>]
        [<Path("article/{Id}")>]
        Article: {| Id: uint32 |} -> Task<Article>

        [<Path("article")>]
        PostArticle: PostArticle -> Task<PostArticleResponse>

        [<Path("account/login")>]
        Login: LoginModel -> Task<LoginResponse>

        [<Path("account/logout")>]
        Logout: unit -> Task<GenericResponse>

        [<Path("account/changePassword")>]
        ChangePassword: ChangePasswordModel -> Task<GenericResponse>

        [<Method("GET")>]
        [<Path("account/details")>]
        AccountDetails: unit -> Task<AccountDetailsResponse>

        [<Path("account/refreshToken")>]
        RefreshToken: unit -> Task<LoginResponse>
    }
    with
        override _.Equals(yobj) =
            match yobj with
            | :? Api -> true
            | _ -> false

        override _.GetHashCode() = 0
        interface System.IComparable with
            member _.CompareTo yobj =
                match yobj with
                | :? Api -> 0
                | _ -> invalidArg "yobj" "cannot compare values of different types"

        #if DEBUG
        static member BaseUri = Uri("http://localhost:7777/api/v1/")
        #else
        static member BaseUri = Uri("https://dogpark.dev/api/v1/")
        #endif

type View = Template<"./wwwroot/templates/realIndex.html">

type NavLink = Template<"./wwwroot/templates/navLink.html">
type NavButton = Template<"./wwwroot/templates/navButton.html">
type ErrorNotification = Template<"./wwwroot/templates/errorNotification.html">
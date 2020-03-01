module DogPark.Handlers

open DogPark.Authentication
open FSharp.Control.Tasks.V2.ContextInsensitive
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Identity
open Microsoft.Extensions.Logging
open System
open System.IO
open System.Text
open System.Threading.Tasks
open DogPark.Api

type Handlers(api : Api) =
    member this.Error (ex : Exception) (logger : ILogger) : HttpHandler =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text "Something went wrong!"

    member this.FinishEarly : HttpFunc = Some >> Task.FromResult

    member this.NotFound = RequestErrors.notFound (text String.Empty) this.FinishEarly

    member this.ShowErrors (errors : IdentityError seq) =
        errors
        |> Seq.fold (fun acc err ->
            sprintf "Code: %s, Description: %s" err.Code err.Description
            |> acc.AppendLine : StringBuilder) (StringBuilder(""))
        |> (fun x -> x.ToString())
        |> text

    member this.GenericSignedInCheck (handler : 'a -> HttpHandler) (view : bool -> 'a) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) -> task {
            return! handler (view ctx.User.Identity.IsAuthenticated) next ctx
        }

    member this.RegisterHandler : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model       = ctx.BindFormAsync<LoginModel>()
                let  user        = User(UserName = model.UserName)
                let  userManager = ctx.GetService<UserManager<User>>()
                let! result      = userManager.CreateAsync(user, model.Password)

                match result.Succeeded with
                | false -> return! this.ShowErrors result.Errors next ctx
                | true  ->
                    let signInManager = ctx.GetService<SignInManager<User>>()
                    do! signInManager.SignInAsync(user, true)
                    return! redirectTo false "/user" next ctx
            }

    member this.LoginHandler : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let! model = ctx.BindFormAsync<LoginModel>()
                let signInManager = ctx.GetService<SignInManager<User>>()
                let! result = signInManager.PasswordSignInAsync(model.UserName, model.Password, true, false)
                match result.Succeeded with
                | true  -> return! redirectTo false "/user" next ctx
                | false -> return! htmlView (Views.loginPage true) next ctx
            }

    member this.UserHandler : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let userManager = ctx.GetService<UserManager<User>>()
                let! user = userManager.GetUserAsync ctx.User
                return! (user |> Views.userPage |> htmlView) next ctx
            }

    member this.MustBeAdmin = (requiresRole "Admin" (text "You are not an administrator"))

    member this.MustBeLoggedIn : HttpHandler =
        requiresAuthentication (redirectTo false "/login")

    member this.LogoutHandler : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let signInManager = ctx.GetService<SignInManager<User>>()
                do! signInManager.SignOutAsync()
                return! (redirectTo false "/") next ctx
            }

    member this.ShowArticle(article : Task<Article option>) : HttpHandler = 
        fun next ctx -> task {
            let! article = article
            match article with
            | Some article -> return! htmlView (Views.articleView ctx.User.Identity.IsAuthenticated article) next ctx
            | None ->  return! this.NotFound ctx
        }

    member this.ShowArticleById (id : int) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) -> task {
            let article = api.GetArticleById id
            return! this.ShowArticle article next ctx
        }

    member this.ShowArticleList : HttpHandler =
        fun next ctx -> task {
            let! articles = api.GetAllDbArticles
            return!
                htmlView
                    (articles
                    |> Seq.map Views.articleListItem
                    |> Views.articleListTable
                    |> List.singleton
                    |> Views.layout ctx.User.Identity.IsAuthenticated)
                    next 
                    ctx         
        }

    member this.RedirectShortUrl (short : string) : HttpHandler =
        fun next ctx -> task { 
            let! long = api.TryFindLongUrlFromShortUrl short
            match long with
            | Some long -> return! redirectTo true long next ctx
            | None -> return! RequestErrors.notFound (text (sprintf "Short URL '%s' does not exist on this server." short)) next ctx
        }

    member this.GetArticle (article : DBArticle) : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) -> task {
            let path = Path.Combine(articleRoot, article.FilePath)

            if File.Exists path then
                let! article = api.ReadArticle article
                return! negotiate article next ctx
            else
                return! this.NotFound ctx
        }

    member this.GetArticleById (id : int) : HttpHandler = 
        fun next ctx -> task {
            let! article = api.GetDbArticleById id
            match article with
            | Some article -> 
                return! this.GetArticle article next ctx
            | None -> 
                return! this.NotFound ctx
        }
    
    member this.CreateShortUrl : HttpHandler =
        fun next ctx -> task {
            let! long = ctx.BindFormAsync<ShortenUrlPostData>()
            match tryMakeUrl long.LongUrl with
            | Ok uri ->
                let! result = api.CreateShortUrl uri.AbsoluteUri
                match result with
                | Ok short ->
                    return! htmlView (Views.urlShortenerSuccess ctx.User.Identity.IsAuthenticated short) next ctx
                | Error err ->
                    return! ServerErrors.internalError (text err) next ctx
            | Error err ->
                return! RequestErrors.badRequest (text (sprintf "'%s' is not a valid URL.\n%s" long.LongUrl err)) next ctx
        }
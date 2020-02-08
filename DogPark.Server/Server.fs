module DogPark.App

open DogPark.Api
open DogPark.Authentication
open DogPark.Handlers
open Giraffe
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.AspNetCore.Identity
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System

// ---------------------------------
// Web app
// ---------------------------------

let makeWebApp (handler : Handlers) =
    choose [
        GET >=>
            choose [
                routef "/@%s" handler.RedirectShortUrl
                routef "/article/%i" handler.ShowArticleById

                route "/articles" >=> handler.ShowArticleList
                route "/"         >=> redirectTo true "/article/1"
                route "/home"     >=> redirectTo true "/article/1"
                route "/about"    >=> handler.GenericSignedInCheck htmlView Views.about

                
                route "/register" >=> htmlView Views.registerPage
                route "/login"    >=> htmlView (Views.loginPage false)

                route "/logout"   >=> handler.MustBeLoggedIn >=> handler.LogoutHandler
                route "/user"     >=> handler.MustBeLoggedIn >=> handler.UserHandler
            ]
        POST >=>
            choose [
                route "/register" >=> handler.RegisterHandler
                route "/login"    >=> handler.LoginHandler
            ]
        choose [
            subRoute "/api" (
                choose [
                    GET >=> choose [
                        routef "/article/%i" handler.GetArticleById
                    ]
                ])
            route "/shorten" >=>
                choose [
                    GET >=> handler.GenericSignedInCheck htmlView (fun signedIn -> Views.layout signedIn [ Views.urlShortenerForm ])
                    POST >=> route "/shorten" >=> (requiresRole "Admin" (text "You are not an administrator")) >=> handler.CreateShortUrl
                ]
        ]
        setStatusCode 404 >=> text "Not Found" 
    ]

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp (handlers : Handlers) (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler handlers.Error)
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseForwardedHeaders()
        .UseAuthentication()
        .UseGiraffe(makeWebApp handlers)

let configureServices (services : IServiceCollection) =
    ignore <| services.AddTransient<IUserStore<User>, MariaDBStore>()
    ignore <| services.AddTransient<IRoleStore<Role>, MariaDBRoleStore>()
    ignore <| 
        services
            .AddIdentity<User, Role>(
                fun options ->
                    // Password settings
                    options.Password.RequiredLength <- 8
            )
            .AddDefaultTokenProviders()

    ignore <|
        services.ConfigureApplicationCookie(
            fun options ->
                options.ExpireTimeSpan <- TimeSpan.FromDays 150.0
                options.LoginPath <- PathString "/login"
                options.LogoutPath <- PathString "/logout"
        )

    ignore <| services.AddCors()        
    ignore <| services.AddGiraffe()
    ignore <| services.Configure<ForwardedHeadersOptions>(fun (options : ForwardedHeadersOptions) -> 
        options.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto)

let configureLogging (builder : ILoggingBuilder) =
    builder.AddFilter(fun l -> l.Equals LogLevel.Error)
           .AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main args =
    let config = 
        ConfigurationBuilder()
            .AddEnvironmentVariables(prefix = "ASPNETCORE_")
            .AddCommandLine(args)
            .Build()

    for test in config.AsEnumerable() do
        printfn "%A" test

    let configureApp = 
        "MariaDB"
        |> config.GetValue
        |> Api
        |> Handlers
        |> configureApp

    WebHost
        .CreateDefaultBuilder()
        .UseConfiguration(config)
        .UseKestrel()
        .UseContentRoot(contentRoot)
        .UseWebRoot(webRoot)
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0
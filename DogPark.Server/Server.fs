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
open Microsoft.Extensions.Hosting.WindowsServices
open System
open Serilog
open Serilog.AspNetCore
open Serilog.Events

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
                route "/account"  >=> handler.MustBeLoggedIn >=> handler.UserHandler

                route "/tetris"   >=> htmlView (Views.tetrisView false)
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
                    GET  >=> handler.MustBeAdmin >=> htmlView (Views.urlShortenerPage true)
                    POST >=> handler.MustBeAdmin >=> handler.CreateShortUrl
                ]
        ]
        setStatusCode 404 >=> text "Not Found"
    ]

// ---------------------------------
// Config and Main
// ---------------------------------


let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins("http://localhost:8080")
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
    services
        .AddTransient<IUserStore<User>, MariaDBStore>()
        .AddTransient<IRoleStore<Role>, MariaDBRoleStore>()
        .ConfigureApplicationCookie(
            fun options ->
                options.ExpireTimeSpan <- TimeSpan.FromDays 150.0
                options.LoginPath <- PathString "/login"
                options.LogoutPath <- PathString "/logout"
        )
        .AddCors()
        .Configure<ForwardedHeadersOptions>(fun (options : ForwardedHeadersOptions) ->
            options.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto)
        .AddGiraffe()
        .AddIdentity<User, Role>(
            fun options ->
                // Password settings
                options.Password.RequiredLength <- 8
        )
        .AddDefaultTokenProviders()
    |> ignore

let configureLogging() =
    Log.Logger <-
        LoggerConfiguration()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(restrictedToMinimumLevel = LogEventLevel.Debug, theme = Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
            .WriteTo.File(System.IO.Path.Combine(logRoot, "server.log"), restrictedToMinimumLevel = LogEventLevel.Verbose, rollingInterval = RollingInterval.Day)
            .CreateLogger()

[<EntryPoint>]
let main args =
    configureLogging()

    let config =
        ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build()

    let configureApp =
        "mariadb"
        |> config.GetValue
        |> Api
        |> Handlers
        |> configureApp

    for var in config.AsEnumerable() do
        Log.Debug(sprintf "%A" var)

    try
        try
            Host
                .CreateDefaultBuilder()
                .ConfigureWebHostDefaults(fun webHostBuilder ->
                    webHostBuilder
                        .UseConfiguration(config)
                        .Configure(configureApp)
                        .ConfigureServices(configureServices)
                        .UseSerilog()
                        .UseKestrel()
                        .UseWebRoot(webRoot)
                    |> ignore
                )
                .UseContentRoot(contentRoot)
                .UseWindowsService()
                .Build()
                .Run()
        with
        | ex ->
            Log.Fatal(ex, "Host terminated unexpectedly")
    finally
        Log.CloseAndFlush()
    0
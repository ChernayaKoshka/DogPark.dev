module DogPark.App

open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open System
open Microsoft.AspNetCore.HttpOverrides

// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [
        GET >=>
            choose [
                routef "/@%s" Handlers.redirectShortUrl
                routef "/article/%i" Handlers.showArticleById

                route "/articles" >=> Handlers.showArticleList
                route "/"         >=> redirectTo true "/article/1"
                route "/home"     >=> redirectTo true "/article/1"
                route "/about"    >=> htmlView Views.about
            ]
        choose [
            subRoute "/api" (
                choose [
                    GET >=> choose [
                        routef "/article/%i" Handlers.Api.getArticleById
                    ]
                ])
            route "/shorten" >=>
                choose [
                    GET >=> htmlView (Views.layout [ Views.urlShortenerForm ])
                    POST >=> route "/shorten" >=> Handlers.Api.createShortUrl
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

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler Handlers.error)
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseForwardedHeaders()
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
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

    WebHostBuilder()
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
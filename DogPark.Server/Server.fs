module DogPark.App

open DogPark.Authentication
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
open System.IO
open Serilog
open Serilog.AspNetCore
open Serilog.Events
open Microsoft.Extensions.FileProviders

// ---------------------------------
// Web app
// ---------------------------------

let webApp =
    choose [
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

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler (fun e l -> e |> string |> text))
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseStaticFiles(getBlazorFrameworkStaticFileOptions (new PhysicalFileProvider(blazorFramework)) "/_framework")
        .UseForwardedHeaders()
        .UseAuthentication()
        .UseGiraffe(webApp)

let configureServices (config: IConfigurationRoot) (services : IServiceCollection) =
    services
        .Configure<ForwardedHeadersOptions>(
            fun (options: ForwardedHeadersOptions) ->
                options.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto
        )
        .AddSingleton<Queries>(
            fun _ -> Queries(config.["MariaDB"])
        )
        .AddTransient<IUserStore<User>, MariaDBStore>(
            fun sp ->
                let queries = sp.GetRequiredService<Queries>()
                new MariaDBStore(queries)
        )
        .AddTransient<IRoleStore<Role>, MariaDBRoleStore>(
            fun sp ->
                let queries = sp.GetRequiredService<Queries>()
                new MariaDBRoleStore(queries)
        )
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
                options.Password.RequiredLength <- 16
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

type EExitCode =
    | Success = 0
    | Failure = -1
    | ConfigurationFailure = -2

[<EntryPoint>]
let main args =
    configureLogging()

    let config =
        try
            ConfigurationBuilder()
                .AddJsonFile((
                                #if DEBUG
                                "appsettings.test.json"
                                #else
                                if RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
                                    "appsettings.json"
                                else
                                    "appsettings.windows.json"
                                #endif
                ), false)
                .AddCommandLine(args)
                .Build()
        with
        | e ->
            Log.Fatal(e, "ConfigurationException: ")
            Log.CloseAndFlush()
            exit -2

    if config.GetValue<bool> "validateconfig" then
        exit 0

    for var in config.AsEnumerable() do
        Log.Debug(sprintf "%A" var)

    try
        try
            Host
                .CreateDefaultBuilder()
                .UseContentRoot(contentRoot)
                .ConfigureWebHostDefaults(fun webHostBuilder ->
                    webHostBuilder
                        .UseWebRoot(webRoot)
                        .UseConfiguration(config)
                        .Configure(configureApp)
                        .ConfigureServices(configureServices config)
                        .UseSerilog()
                        .UseKestrel()
                    |> ignore
                )
                .Build()
                .Run()
            0
        with
        | ex ->
            Log.Fatal(ex, "Host terminated unexpectedly")
            -1
    finally
        Log.CloseAndFlush()
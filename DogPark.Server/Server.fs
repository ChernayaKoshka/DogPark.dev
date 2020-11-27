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
open Serilog.Sinks.MariaDB
open Serilog.Sinks.MariaDB.Extensions
open Serilog.AspNetCore
open Serilog.Events
open Microsoft.Extensions.FileProviders
open FSharp.Control.Tasks.V2.ContextInsensitive
open System.Runtime.InteropServices
open System.Threading.Tasks

// ---------------------------------
// Web app
// ---------------------------------

let error (msg: string) = json {| Error = msg |}

let handleArticle: HttpHandler =
    fun next ctx -> task {
        match ctx.GetQueryStringValue "id" with
        | Ok id ->
            match Int32.TryParse(id) with
            | (true, id) ->
                let queries = ctx.GetService<Queries>()
                let! article = queries.GetArticleById id
                return! json article next ctx
            | (false, _) ->
                return! RequestErrors.badRequest (error "Parameter 'id' must be an integer.") next ctx
        | Error err ->
            return! RequestErrors.badRequest (error err) next ctx
    }

let seed: HttpHandler =
    fun next ctx -> task {
        let userManager = ctx.GetService<UserManager<User>>()

        let! user = userManager.FindByNameAsync "admin"
        let! user =
            if isNull user then
                task {
                    let user = User(UserName = "admin")
                    let! result = userManager.CreateAsync(user, "b*^tDAqOhkwaZ5kLHlHl$2e9b%WtX%5a4GuUrN1VuPq$xR0VDnzsej#^6UWAz!qI#B@wJ8G0QE$D7%@EgDhV&qanc8LL@oYQz5W")
                    if result.Succeeded then return Ok user
                    else return Error result
                }
            else
                Task.FromResult (Ok user)

        match user with
        | Ok user ->
            return! text "seeded" next ctx
        | Error err ->
            return! ServerErrors.internalError (json err) next ctx
    }

let mustBeLocal: HttpHandler =
    fun next ctx -> task {
        if ctx.Connection.RemoteIpAddress = ctx.Connection.LocalIpAddress then
            return! next ctx
        else
            return! RequestErrors.UNAUTHORIZED null "DogPark" String.Empty earlyReturn ctx
    }

let isSignedIn (ctx: HttpContext) =
    (isNull >> not) ctx.User && ctx.User.Identity.IsAuthenticated

let loginHandler: HttpHandler =
    fun next ctx -> task {
        if isSignedIn ctx then
            return! setStatusCode StatusCodes.Status200OK next ctx
        else
            let! model = ctx.BindJsonAsync<LoginModel>()
            let signInManager = ctx.GetService<SignInManager<User>>()
            let! result = signInManager.PasswordSignInAsync(model.Username, model.Password, true, false)
            if result.Succeeded then
                return! setStatusCode StatusCodes.Status200OK next ctx
            else
                return! (setStatusCode StatusCodes.Status401Unauthorized >=> json result) next ctx
    }

let logoutHandler: HttpHandler =
    fun next ctx -> task {
        let signInManager = ctx.GetService<SignInManager<User>>()
        do! signInManager.SignOutAsync()
        return! setStatusCode StatusCodes.Status200OK next ctx
    }

let mustBeLoggedIn: HttpHandler =
    error "You must be logged in."
    |> RequestErrors.unauthorized "Identity" "DogPark"

let webApp =
    choose [
        subRoute "/api"(
            choose [
                subRoute "/v1" (
                    choose [
                        GET >=> choose [
                            route "/article" >=> handleArticle
                            route "/ip" >=> fun next ctx -> text (string ctx.Connection.RemoteIpAddress) next ctx
                            routeCi "/Am/I/local" >=> mustBeLocal >=> text "you're local"
                            routeCi "/Am/I/loggedin" >=> requiresAuthentication (text "no") >=> text "yes"
                        ]
                        POST >=> choose [
                            route "/seed" >=> mustBeLocal >=> seed
                            route "/login" >=> loginHandler
                            route "/logout" >=> logoutHandler
                        ]
                    ]
                )
                RequestErrors.notFound (error "No such API")
            ]
        )
        GET >=> htmlFile "./wwwroot/index.html"
        setStatusCode 404 >=> text "Not Found"
    ]

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins("http://localhost:7777")
        .AllowAnyMethod()
        .AllowAnyHeader()
    |> ignore

let configureApp (app : IApplicationBuilder) =
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler (fun e l -> ServerErrors.internalError (text "Something went wrong") ))
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

let configureLogging (config: IConfigurationRoot) =
    Log.Logger <-
        LoggerConfiguration()
            .MinimumLevel.Verbose()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(restrictedToMinimumLevel = LogEventLevel.Debug, theme = Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code)
            .WriteTo.File(System.IO.Path.Combine(logRoot, "server.log"), restrictedToMinimumLevel = LogEventLevel.Verbose, rollingInterval = RollingInterval.Day)
            .WriteTo.MariaDB(
                connectionString = config.["MariaDB"],
                tableName = "logs",
                autoCreateTable = true,
                useBulkInsert = false
            )
            .CreateLogger()

type EExitCode =
    | Success = 0
    | Failure = -1
    | ConfigurationFailure = -2

[<EntryPoint>]
let main args =
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
            exit -2

    if config.GetValue<bool> "validateconfig" then
        exit 0

    configureLogging config

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
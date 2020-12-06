module DogPark.App

open DogPark.Shared
open DogPark.Authentication
open Giraffe
open Giraffe.Serialization.Json
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
open System.Text.Json
open Serilog.Context
open Microsoft.Net.Http.Headers
open System.Net
open Microsoft.AspNetCore.StaticFiles
open System.Net.Http

// ---------------------------------
// Web app
// ---------------------------------

let error (msg: string) = json { Success = false; Message = msg }
let jmessage (msg: string) = json { Success = true; Message = msg }
let jnotauthorized o = RequestErrors.unauthorized "Identity" "DogPark" (json o)

let handleArticle (idArticle: uint32): HttpHandler =
    fun next ctx -> task {
        let queries = ctx.GetService<Queries>()
        match! queries.GetArticleById idArticle with
        | Ok article ->
            return! json article next ctx
        | Error err ->
            return! RequestErrors.badRequest (error err) next ctx
    }

let getAllArticles: HttpHandler =
    fun next ctx -> task {
        let queries = ctx.GetService<Queries>()
        let! details = queries.GetAllArticleDetails()
        return! json details next ctx
    }

let seed: HttpHandler =
    fun next ctx -> task {
        let userManager = ctx.GetService<UserManager<User>>()

        let! user = userManager.FindByNameAsync "admin"
        let! user =
            if isNull user then
                task {
                    let user = User(UserName = "admin")
                    let! result = userManager.CreateAsync(user, "Change_me_asap20!")
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
            return! RequestErrors.unauthorized null "DogPark" (error "You're not local.") earlyReturn ctx
    }

let isSignedIn (ctx: HttpContext) =
    (isNull >> not) ctx.User && ctx.User.Identity.IsAuthenticated

let loginHandler: HttpHandler =
    fun next ctx -> task {
        if isSignedIn ctx then
            return! jmessage "success" next ctx
        else
            let! model = ctx.BindJsonAsync<LoginModel>()
            let signInManager = ctx.GetService<SignInManager<User>>()
            let! result = signInManager.PasswordSignInAsync(model.Username, model.Password, true, false)
            if result.Succeeded then
                let userManager = ctx.GetService<UserManager<User>>()
                let! user = userManager.FindByNameAsync(model.Username)
                return! json { Success = true; Details = Some { Username = user.UserName }; Message = None } next ctx
            else
                return! jnotauthorized { Success = false; Details = None; Message = Some "Sign in failed." } next ctx
    }

let logoutHandler: HttpHandler =
    fun next ctx -> task {
        let signInManager = ctx.GetService<SignInManager<User>>()
        do! signInManager.SignOutAsync()
        return! setStatusCode StatusCodes.Status200OK next ctx
    }

let changePassword: HttpHandler =
    fun next ctx -> task {
        if isSignedIn ctx then
            let! model = ctx.BindJsonAsync<ChangePasswordModel>()
            let userManager = ctx.GetService<UserManager<User>>()
            let! user = userManager.FindByNameAsync(ctx.User.Identity.Name)

            if isNull user then
                return! ServerErrors.internalError (error $"Could not locate user {ctx.User.Identity.Name}") next ctx
            else
                let! result = userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword)
                if result.Succeeded then
                    return! setStatusCode StatusCodes.Status200OK next ctx
                else
                    return! jnotauthorized result next ctx
        else
            return! RequestErrors.unauthorized null "DogPark" (error "You're not authorized.") earlyReturn ctx
    }

let mustBeLoggedIn: HttpHandler =
    error "You must be logged in."
    |> RequestErrors.unauthorized "Identity" "DogPark"

let begoneBot =
    [|0x0..0xD7FF|]
    // reversing to make sure any closing brackets and whatnot are in opposite order (eg: ][) to (hopefully) fuck the bot
    |> Array.rev
    |> Array.map char
    |> String
    |> sprintf """</Begone, bot!>%s<Begone, bot!>"""

let webApp =
    choose [
        routeCix
            """(?:Nimbostratus|[pP][hH][pP][mM][yY][aA][dD][mM][iI][nN]|/+wp-login\.php|/.git/HEAD|/TP/public/index\.php|/admin/login\.php|/allstat\.php|/cfg/|/cisco/|/config.*/|/firmware/|/linksys/|/login\.cgi|/phone/|/polycom/|/provision.*/|/run\.py|/struts|/wls-wsat|/wp-config\.php|/wuwu11\.php|/wwwroot\.rar|/rpc/trackback/)"""
            >=> text begoneBot

        subRoute "/api"(
            choose [
                subRoute "/v1" (
                    choose [
                        GET >=> choose [
                            route "/ping" >=> text "pong"

                            route "/article"
                                >=> publicResponseCaching (int (TimeSpan.FromHours(1.).TotalSeconds)) None
                                >=> getAllArticles

                            routef "/article/%d" (fun (id: int64) ->
                                publicResponseCaching (int (TimeSpan.FromHours(1.).TotalSeconds)) None
                                >=> handleArticle (uint32 id)
                            )

                            route "/ip" >=> fun next ctx -> text (string ctx.Connection.RemoteIpAddress) next ctx
                            route "/am/i/local" >=> mustBeLocal >=> text "you're local"
                            route "/am/i/loggedin" >=> requiresAuthentication (text "no") >=> text "yes"
                        ]
                        POST >=> choose [
                            route "/seed" >=> mustBeLocal >=> seed
                            subRoute "/account" (
                                choose [
                                    route "/login" >=> loginHandler
                                    route "/logout" >=> logoutHandler
                                    route "/changepassword" >=> requiresAuthentication mustBeLoggedIn >=> changePassword
                                ]
                            )
                        ]
                    ]
                )
                RequestErrors.notFound (error "No such API")
            ]
        )
        GET >=> htmlFile (Path.Combine(webRoot, "index.html"))
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
    let provider = FileExtensionContentTypeProvider()
    provider.Mappings.[".txt"] <- "text/plain;charset=utf-8"

    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app.UseGiraffeErrorHandler (
            fun e l ->
                Log.Error(e, "Unhandled exception!")
                ServerErrors.internalError (text "Something went wrong")
        )
    )
        .Use(fun (ctx: HttpContext) (next: Func<Task>) ->
            let task = task {
                Log.Debug("X-Forwarded-For: {Forwarded}", ctx.Request.Headers.["X-Forwarded-For"])
                do! next.Invoke()
            }
            task :> Task
        )
        .UseForwardedHeaders()
        .Use(fun (ctx: HttpContext) (next: Func<Task>) ->
            let task = task {
                use _ = (LogContext.PushProperty("IPAddress", ctx.Connection.RemoteIpAddress))
                use _ = (LogContext.PushProperty("UserAgent", ctx.Request.Headers.[HeaderNames.UserAgent]))
                do! next.Invoke()
            }
            task :> Task
        )
        .UseSerilogRequestLogging(@"{IPAddress}:{UserAgent} HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms")
        .UseCors(configureCors)
        .UseResponseCaching()
        .UseStaticFiles(
            StaticFileOptions(
                FileProvider = new PhysicalFileProvider(webRoot),
                ContentTypeProvider = provider
            )
        )
        .UseBlazorFrameworkFiles()
        .UseAuthentication()

        .UseGiraffe(webApp)

let configureServices (config: IConfigurationRoot) (services : IServiceCollection) =
    services
        .Configure<ForwardedHeadersOptions>(
            fun (options: ForwardedHeadersOptions) ->
                options.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto

                Log.Debug("Fetching latest IP ranges from Cloudflare")
                use client = new HttpClient(BaseAddress = Uri("https://www.cloudflare.com/"))
                use response = client.Send(new HttpRequestMessage(HttpMethod.Get, "ips-v4")).EnsureSuccessStatusCode()
                use reader = new StreamReader(response.Content.ReadAsStream())
                reader
                    .ReadToEnd()
                    .Replace("\r", "")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                |> Array.map (fun ip ->
                    Log.Debug("Adding '{IP}' from response", ip)
                    let [| ip; range |] = ip.Split('/')
                    IPNetwork(IPAddress.Parse(ip), int range)
                )
                |> Array.iter options.KnownNetworks.Add

                // I'm behind Cloudflare _and_ Caddy, meaning we need to read two entries
                options.ForwardLimit <- 2
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
        .AddSingleton<IJsonSerializer>(SystemTextJsonSerializer(JsonSerializerOptions()))
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
            #if !DEBUG
            .WriteTo.MariaDB(
                connectionString = config.["MariaDB"],
                tableName = "logs",
                autoCreateTable = true,
                useBulkInsert = false
            )
            #endif
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
            printfn "%A" e
            exit -2

    if config.GetValue<bool> "validateconfig" then
        exit 0

    configureLogging config

    Directory.SetCurrentDirectory(contentRoot)

    for var in config.AsEnumerable() do
        Log.Debug(sprintf "%A" var)

    try
        try
            Host
                .CreateDefaultBuilder()
                .ConfigureWebHostDefaults(fun webHostBuilder ->
                    webHostBuilder
                        #if DEBUG
                        .UseStaticWebAssets()
                        #else
                        .UseContentRoot(contentRoot)
                        .UseWebRoot(webRoot)
                        #endif
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
module DogPark.App

open DogPark.Shared
open DogPark.Authentication
open Giraffe
open Giraffe.Razor
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
open FSharp.Control.Tasks
open System.Runtime.InteropServices
open System.Threading.Tasks
open System.Text.Json
open Serilog.Context
open Microsoft.Net.Http.Headers
open System.Net
open Microsoft.AspNetCore.StaticFiles
open System.Net.Http
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open System.Security.Claims
open System.Security.Cryptography
open System.Text.Json

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Bolero
open Bolero.Remoting.Server
open Bolero.Server
// open Bolero.Templating.Server


// ---------------------------------
// Web app
// ---------------------------------

let error (msg: string) = json { Success = false; Message = msg }
let jmessage (msg: string) = json { Success = true; Message = msg }
let jnotauthorized o = RequestErrors.unauthorized "JWT" "DogPark" (json o)

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

let jwtRefreshTokenCookiePath = "/api/v1/account/"

let setJwtRefreshTokenCookie (ctx: HttpContext) token =
    ctx.Response.Cookies.Append(
        "JwtRefreshToken",
        token.RefreshToken.TokenString,
        CookieOptions(
            HttpOnly = true,
            Secure = true,
            Expires = DateTimeOffset(token.RefreshToken.ExpireAt),
            Path = jwtRefreshTokenCookiePath
        )
    )

let loginHandler: HttpHandler =
    fun next ctx -> task {
        let! model = ctx.BindJsonAsync<LoginModel>()
        let signInManager = ctx.GetService<SignInManager<User>>()
        let! user = signInManager.UserManager.FindByNameAsync(model.Username)
        if isNull user then
            return! jnotauthorized { Success = false; Details = None; Message = Some "Sign in failed." } next ctx
        else

        let! result = signInManager.CheckPasswordSignInAsync(user, model.Password, false)
        if result.Succeeded then
            let jwtAuthManager = ctx.GetService<JwtAuthManager>()
            let token = jwtAuthManager.GenerateTokens user.UserName [| Claim(ClaimTypes.Name, user.UserName) |] DateTime.Now
            setJwtRefreshTokenCookie ctx token
            return! json { Success = true; Details = Some { Username = user.UserName; Jwt = token }; Message = None } next ctx
        else

        return! jnotauthorized { Success = false; Details = None; Message = Some "Sign in failed." } next ctx
    }

let logoutHandler: HttpHandler =
    fun next ctx -> task {
        if isSignedIn ctx |> not then
            return! jmessage "success" next ctx
        else
        let jwtAuthManager = ctx.GetService<JwtAuthManager>()

        ctx.Response.Cookies.Delete("JwtRefreshToken", CookieOptions(Path = jwtRefreshTokenCookiePath))

        ctx.User.Identity.Name
        |> jwtAuthManager.Logout
        |> ignore

        return! jmessage "success" next ctx
    }

let mustBeLoggedIn: HttpHandler =
    error "You must be logged in."
    |> RequestErrors.unauthorized "JWT" "DogPark"

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
            return! mustBeLoggedIn next ctx
    }

let accountDetails: HttpHandler =
    fun next ctx -> task {
        if isSignedIn ctx then
            return! json { AccountDetailsResponse.Success = true; Details = Some { Username = ctx.User.Identity.Name }; Message = None } next ctx
        else
            return! mustBeLoggedIn next ctx
    }

let refreshTokenHandler: HttpHandler =
    fun next ctx -> task {
        try
            match ctx.TryGetRequestHeader HeaderNames.Authorization, ctx.GetCookieValue "JwtRefreshToken" with
            | Some bearer, Some refreshToken when bearer.StartsWith("Bearer ") ->
                let token = bearer.["Bearer ".Length..]
                let jwtAuthManager = ctx.GetService<JwtAuthManager>()
                match jwtAuthManager.Refresh refreshToken token DateTime.Now with
                | Some refreshed ->
                    setJwtRefreshTokenCookie ctx refreshed
                    let decoded = jwtDecodeNoVerify refreshed.AccessToken
                    let nameClaim = decoded.Claims |> Seq.find (fun c -> c.Type = ClaimTypes.Name)
                    return! json { Success = true; Details = Some { Username = nameClaim.Value; Jwt = refreshed }; Message = None } next ctx
                | None ->
                    return! RequestErrors.unauthorized "JWT" "DogPark" (error "Refresh token was either expired or otherwise invalid") next ctx
            | _ ->
                return! RequestErrors.unauthorized "JWT" "DogPark" (error "Invalid authorization header or JwtRefreshToken cookie") next ctx
        with
        | :? JsonException ->
            return! RequestErrors.unauthorized "JWT" "DogPark" (error "Refresh token missing or malformed") next ctx
    }

let postArticle: HttpHandler =
    fun next ctx -> task {
        let! model = ctx.BindJsonAsync<PostArticle>()

        match model.HasErrors() with
        | Some errors ->
            return! error (String.concat "\n" errors) next ctx
        | None ->
            let userManager = ctx.GetService<UserManager<User>>()
            let! user = userManager.FindByNameAsync(ctx.User.Identity.Name)
            let filename = $"""{sanitizeFilename (model.Headline.Trim())}-{Guid.NewGuid().ToString("N")}.md"""
            let path = Path.Combine(articleRoot, filename)
            do! File.WriteAllTextAsync(path, model.Content.Trim())

            let queries = ctx.GetService<Queries>()
            let! idArticle = queries.InsertArticle user.IDUser (model.Headline.Trim()) filename
            return! json { Success = true; Id = Some idArticle; Message = None } next ctx
    }

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
            """(?:\.sql|\.[pP][hH][pP]|/\.git/HEAD|/\.env|/cfg/|/cisco/|/config.*/|/firmware/|/linksys/|/login\.cgi|/phone/|/polycom/|/provision.*/|/run\.py|/struts|/wls-wsat|/wwwroot\.rar|/rpc/trackback/)"""
            >=> htmlString begoneBot

        subRoute "/api"(
            choose [
                subRoute "/v1" (
                    choose [
                        GET >=> choose [
                            routeCi "/ping" >=> text "pong"

                            routeCi "/article"
                                >=> publicResponseCaching (int (TimeSpan.FromHours(1.).TotalSeconds)) None
                                >=> getAllArticles

                            routeCif "/article/%d" (fun (id: int64) ->
                                publicResponseCaching (int (TimeSpan.FromHours(1.).TotalSeconds)) None
                                >=> handleArticle (uint32 id)
                            )

                            routeCi "/ip" >=> fun next ctx -> text (string ctx.Connection.RemoteIpAddress) next ctx
                            routeCi "/am/i/local" >=> mustBeLocal >=> text "you're local"
                            routeCi "/am/i/loggedIn" >=> requiresAuthentication (error "You're not logged in.") >=> jmessage "You're logged in."
                            routeCi "/account/details" >=> requiresAuthentication (error "You're not logged in.") >=> accountDetails
                            
                            routeCif "/smorpa/%i" HttpHandlers.Api.Smorpa.getSmorpa
                        ]
                        POST >=> choose [
                            routeCi "/seed" >=> mustBeLocal >=> seed
                            routeCi "/article" >=> requiresAuthentication mustBeLoggedIn >=> postArticle
                            subRouteCi "/account" (
                                choose [
                                    routeCi "/login" >=> loginHandler
                                    routeCi "/logout" >=> logoutHandler
                                    routeCi "/changePassword" >=> requiresAuthentication mustBeLoggedIn >=> changePassword
                                    routeCi "/refreshToken" >=> refreshTokenHandler
                                ]
                            )
                        ]
                    ]
                )
                RequestErrors.notFound (error "No such API")
            ]
        )

        choose [ GET; HEAD ]
        >=> choose [
            routeCi "/"
            routeCi "/Login"
            routeCi "/Articles"
            routeCi "/Editor"
            routeCix @"/Article/\d+"
            routeCi "/Smorpa"
        ]
        >=> razorHtmlView "_Host" None None None

        setStatusCode 404 >=> text "Not Found"
    ]

// ---------------------------------
// Config and Main
// ---------------------------------

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins("http://localhost:7777", "https://localhost:7777", "http://dogpark.dev", "https://dogpark.dev")
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

                #if !DEBUG
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
                #endif
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
    |> ignore

    services
        .AddSingleton<Json.ISerializer>(SystemTextJson.Serializer(jsonOptions))
        .AddIdentity<User, Role>(
            fun options ->
                // Password settings
                options.Password.RequiredLength <- 16
        )
        .AddDefaultTokenProviders()
    |> ignore

    services
        .AddCors()
        .Configure<ForwardedHeadersOptions>(fun (options : ForwardedHeadersOptions) ->
            options.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto)
    |> ignore

    let privateRsaParams, publicRsaParams =
        let keyFromConfig = config.GetValue<string> >> Convert.FromBase64String
        use rsa = new RSACryptoServiceProvider(2048)

        let mutable read = 0
        rsa.ImportRSAPublicKey(ReadOnlySpan<byte>(keyFromConfig "jwtPublicKey"), &read)
        read <- 0
        rsa.ImportRSAPrivateKey(ReadOnlySpan<byte>(keyFromConfig "jwtPrivateKey"), &read)
        rsa.ExportParameters(true), rsa.ExportParameters(false)

    services
        .AddSingleton<JwtAuthManager>(fun _ -> JwtAuthManager(privateRsaParams, "dogpark.dev", "dogpark.dev", TimeSpan.FromMinutes(15.), TimeSpan.FromDays(7.)))
        .AddAuthentication(fun x ->
            x.DefaultAuthenticateScheme <- JwtBearerDefaults.AuthenticationScheme
            x.DefaultChallengeScheme <- JwtBearerDefaults.AuthenticationScheme
        )
        .AddJwtBearer(fun x ->
            x.RequireHttpsMetadata <- true
            x.SaveToken <- true
            x.TokenValidationParameters <-
                TokenValidationParameters(
                    ValidateIssuer = true,
                    ValidIssuer = "dogpark.dev",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = RsaSecurityKey(publicRsaParams),
                    ValidAudience = "dogpark.dev",
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1.)
                )
        )
    |> ignore


    services.AddMvc().AddRazorRuntimeCompilation() |> ignore
    services.AddServerSideBlazor() |> ignore
    services.AddBoleroHost() |> ignore

    services.AddGiraffe()
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
                connectionString = config.["mariaDB"],
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
                .AddJsonFile(
                (
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

    // Directory.SetCurrentDirectory(contentRoot)

    for var in config.AsEnumerable() do
        Log.Debug(sprintf "%A" var)

    try
        try
            Host
                .CreateDefaultBuilder()
                .UseSerilog()
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
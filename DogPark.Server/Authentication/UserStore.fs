[<AutoOpen>]
module DogPark.Authentication.UserStore

open Microsoft.AspNetCore.Identity
open System
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.Extensions.Configuration
open MySqlConnector 
open MySql.Data.MySqlClient
open Dapper

type MariaDBStore(config : IConfiguration) =
    let connectionString = config.GetConnectionString("MariaDB")
    interface IUserStore<User> with
        member __.Dispose(): unit =
            ()

        member __.CreateAsync(user: User, cancellationToken: CancellationToken): Task<IdentityResult> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! id = con.QuerySingleAsync<int>(@"INSERT INTO user (UserName, NormalizedUserName, PasswordHash) VALUES (@UserName, @NormalizedUserName, @PasswordHash); SELECT CAST(last_insert_id() as int)", user)
            user.User <- id
            return IdentityResult.Success
        }

        member __.DeleteAsync(user: User, cancellationToken: CancellationToken): Task<IdentityResult> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! _ = con.ExecuteAsync("DELETE FROM user WHERE User = @User", user)
            return IdentityResult.Success
        }

        member __.FindByIdAsync(userId: string, cancellationToken: CancellationToken): Task<User> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            return! con.QuerySingleOrDefaultAsync<User>("SELECT * FROM user WHERE User = @User", {| User = userId |})
        }

        member __.FindByNameAsync(normalizedUserName: string, cancellationToken: CancellationToken): Task<User> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            return! con.QuerySingleOrDefaultAsync<User>("SELECT * FROM user WHERE NormalizedUserName = @NormalizedUserName", {| NormalizedUserName = normalizedUserName |})
        }

        member __.GetNormalizedUserNameAsync(user: User, cancellationToken: CancellationToken): Task<string> = task {
            cancellationToken.ThrowIfCancellationRequested()
            return user.NormalizedUserName
        }

        member __.GetUserIdAsync(user: User, cancellationToken: CancellationToken): Task<string> = task {
            cancellationToken.ThrowIfCancellationRequested()
            return string user.User
        }

        member __.GetUserNameAsync(user: User, cancellationToken: CancellationToken): Task<string> = task {
            cancellationToken.ThrowIfCancellationRequested()
            return user.UserName
        }

        member __.SetNormalizedUserNameAsync(user: User, normalizedName: string, cancellationToken: CancellationToken): Task =
            user.NormalizedUserName <- normalizedName
            Task.CompletedTask

        member __.SetUserNameAsync(user: User, userName: string, cancellationToken: CancellationToken): Task = 
            user.UserName <- userName
            Task.CompletedTask

        member __.UpdateAsync(user: User, cancellationToken: CancellationToken): Task<IdentityResult> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! _ = con.ExecuteAsync("UPDATE user SET UserName = @UserName, PasswordHash = @PasswordHash WHERE User = @User", user)
            return IdentityResult.Success
        }

    interface IUserPasswordStore<User> with
        member this.GetPasswordHashAsync(user : User, cancellationToken : CancellationToken): Task<string> = 
            Task.FromResult(user.PasswordHash)

        member this.HasPasswordAsync(user: User, cancellationToken: CancellationToken): Task<bool> =
            user.PasswordHash
            |> String.IsNullOrWhiteSpace 
            |> not
            |> Task.FromResult

        member this.SetPasswordHashAsync(user: User, passwordHash: string, cancellationToken: CancellationToken): Task = 
            user.PasswordHash <- passwordHash
            Task.CompletedTask

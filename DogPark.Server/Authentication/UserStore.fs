[<AutoOpen>]
module DogPark.Authentication.UserStore

open Microsoft.AspNetCore.Identity
open System
open System.Threading
open System.Threading.Tasks
open System.Linq
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.Extensions.Configuration
open MySqlConnector
open MySql.Data.MySqlClient
open Dapper
open DogPark

type MariaDBStore(queries: Queries) =
    interface IUserStore<User> with
        member __.Dispose(): unit =
            ()

        member __.CreateAsync(user: User, cancellationToken: CancellationToken): Task<IdentityResult> = task {
            let! id = queries.UserCreate(user, cancellationToken)
            user.IDUser <- id
            return IdentityResult.Success
        }

        member __.DeleteAsync(user: User, cancellationToken: CancellationToken): Task<IdentityResult> = task {
            do! queries.UserDelete(user, cancellationToken)
            return IdentityResult.Success
        }

        member __.FindByIdAsync(userId: string, cancellationToken: CancellationToken): Task<User> =
            queries.UserFindById(int userId, cancellationToken)

        member __.FindByNameAsync(normalizedUserName: string, cancellationToken: CancellationToken): Task<User> =
            queries.UserFindByName(normalizedUserName, cancellationToken)

        member __.GetNormalizedUserNameAsync(user: User, cancellationToken: CancellationToken): Task<string> =
            Task.FromResult(user.NormalizedUserName)

        member __.GetUserIdAsync(user: User, cancellationToken: CancellationToken): Task<string> =
            Task.FromResult(string user.IDUser)

        member __.GetUserNameAsync(user: User, cancellationToken: CancellationToken): Task<string> =
            Task.FromResult(user.UserName)

        member __.SetNormalizedUserNameAsync(user: User, normalizedName: string, cancellationToken: CancellationToken): Task =
            user.NormalizedUserName <- normalizedName
            Task.CompletedTask

        member __.SetUserNameAsync(user: User, userName: string, cancellationToken: CancellationToken): Task =
            user.UserName <- userName
            Task.CompletedTask

        member __.UpdateAsync(user: User, cancellationToken: CancellationToken): Task<IdentityResult> = task {
            do! queries.UserUpdate(user, cancellationToken)
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

    interface IUserRoleStore<User> with
        member this.AddToRoleAsync(user: User, normalizedRoleName: string, cancellationToken: CancellationToken): Task =
            queries.UserAddToRole(user, normalizedRoleName, cancellationToken) :> Task

        member this.GetRolesAsync(user: User, cancellationToken: CancellationToken): Task<Collections.Generic.IList<string>> =
            queries.UserGetRoles(user, cancellationToken)

        member this.GetUsersInRoleAsync(normalizedRoleName: string, cancellationToken: CancellationToken): Task<Collections.Generic.IList<User>> =
            queries.UserGetInRole(normalizedRoleName, cancellationToken)

        member this.IsInRoleAsync(user: User, normalizedRoleName: string, cancellationToken: CancellationToken): Task<bool> =
            queries.UserIsInRole(user, normalizedRoleName, cancellationToken)

        member this.RemoveFromRoleAsync(user: User, normalizedRoleName: string, cancellationToken: CancellationToken): Task =
            queries.UserRemoveFromeRole(user, normalizedRoleName, cancellationToken) :> Task
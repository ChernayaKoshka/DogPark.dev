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

let private roleFromName (con : MySqlConnection) (name : string) = task {
    return! con.QuerySingleAsync<Role>(@"SELECT * FROM role WHERE Name = @Name", {| Name = name |})
}

type MariaDBStore(config : IConfiguration) =
    let connectionString = config.GetValue("MariaDB")
    interface IUserStore<User> with
        member __.Dispose(): unit =
            ()

        member __.CreateAsync(user: User, cancellationToken: CancellationToken): Task<IdentityResult> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! id = con.QuerySingleAsync<int>(@"INSERT INTO user (UserName, NormalizedUserName, PasswordHash) VALUES (@UserName, @NormalizedUserName, @PasswordHash); SELECT CAST(last_insert_id() as int)", user)
            user.IDUser <- id
            return IdentityResult.Success
        }

        member __.DeleteAsync(user: User, cancellationToken: CancellationToken): Task<IdentityResult> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! _ = con.ExecuteAsync("DELETE FROM user WHERE IDUser = @User", user)
            return IdentityResult.Success
        }

        member __.FindByIdAsync(userId: string, cancellationToken: CancellationToken): Task<User> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            return! con.QuerySingleOrDefaultAsync<User>("SELECT * FROM user WHERE IDUser = @User", {| User = userId |})
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
            return string user.IDUser
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
            let! _ = con.ExecuteAsync("UPDATE user SET UserName = @UserName, PasswordHash = @PasswordHash WHERE IDUser = @User", user)
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
        member this.AddToRoleAsync(user: User, roleName: string, cancellationToken: CancellationToken): Task =
            let doThing = task {
                    cancellationToken.ThrowIfCancellationRequested()
                    use con = new MySqlConnection(connectionString)
                    do! con.OpenAsync()
                    let! role = roleFromName con roleName
                    let! _ = con.ExecuteAsync(@"INSERT INTO userrole (IDUser, IDRole) VALUES (@IDUser, @IDRole)", {| IDUser = user.IDUser; IDRole = role.IDRole |})
                    return ()
            }
            doThing.Wait()
            Task.CompletedTask

        member this.GetRolesAsync(user: User, cancellationToken: CancellationToken): Task<Collections.Generic.IList<string>> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! res =
                con.QueryAsync<string>(
                    @"
                    SELECT Name FROM role
                    JOIN userrole ur ON ur.IDRole = role.IDRole
                    WHERE ur.IDUser = @IDUser
                    ",
                    {| IDUser = user.IDUser |})
            return res.ToList() :> Collections.Generic.IList<string>
        }

        member this.GetUsersInRoleAsync(roleName: string, cancellationToken: CancellationToken): Task<Collections.Generic.IList<User>> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! res =
                con.QueryAsync<User>(
                    @"
                    SELECT user.* FROM user user
                    JOIN userrole ur ON ur.IDUser = user.IDUser
                    JOIN role r ON ur.IDRole = r.IDRole
                    WHERE r.Name = @RoleName
                    ",
                    {| RoleName = roleName |})
            return res.ToList() :> Collections.Generic.IList<User>
        }
        member this.IsInRoleAsync(user: User, roleName: string, cancellationToken: CancellationToken): Task<bool> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            return!
                con.QuerySingleAsync<bool>(
                    @"
                    SELECT COUNT(*) > 0 FROM user user
                    JOIN userrole ur ON ur.IDUser = user.IDUser
                    JOIN role r ON ur.IDRole = r.IDRole
                    WHERE r.Name = @RoleName
                    ",
                    {| RoleName = roleName |})
        }

        member this.RemoveFromRoleAsync(user: User, roleName: string, cancellationToken: CancellationToken): Task =
            failwith "Not Implemented"
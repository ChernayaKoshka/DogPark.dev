[<AutoOpen>]
module DogPark.Authentication.RoleStore

open DogPark.Authentication.Types
open Microsoft.AspNetCore.Identity
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open Microsoft.Extensions.Configuration
open MySql.Data.MySqlClient
open Dapper

type MariaDBRoleStore(config : IConfiguration) =
    let connectionString = config.GetValue "MariaDB"
    interface IRoleStore<Role> with
        member this.CreateAsync (role: Role, cancellationToken: CancellationToken) : Task<IdentityResult> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! id = con.QuerySingleAsync<int>(@"INSERT INTO role (Name, NormalizedName) VALUES (@Name, @NormalizedName); SELECT CAST(last_insert_id() as int)", role)
            role.IDRole <- id
            return IdentityResult.Success
        }

        member this.DeleteAsync (role: Role, cancellationToken: CancellationToken) : Task<IdentityResult> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! _ = con.ExecuteAsync("DELETE FROM role WHERE IDRole = @IDRole", role)
            return IdentityResult.Success
        }

        member this.FindByIdAsync (roleId: string, cancellationToken: CancellationToken) : Task<Role> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            return! con.QuerySingleOrDefaultAsync<Role>(@"SELECT * FROM role WHERE IDRole = @IDRole", {| IDRole = int roleId |});
        }

        member this.FindByNameAsync (normalizedRoleName: string, cancellationToken: CancellationToken) : Task<Role> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            return! con.QuerySingleOrDefaultAsync<Role>(@"SELECT * FROM role WHERE NormalizedName = @NormalizedName", {| NormalizedName = normalizedRoleName |});
        }
        member this.GetNormalizedRoleNameAsync (role: Role, cancellationToken: CancellationToken) : Task<string> =
            Task.FromResult role.NormalizedName

        member this.GetRoleIdAsync (role: Role, cancellationToken: CancellationToken) : Task<string> =
            role.IDRole
            |> string
            |> Task.FromResult

        member this.GetRoleNameAsync (role: Role, cancellationToken: CancellationToken) : Task<string> =
            Task.FromResult role.Name

        member this.SetNormalizedRoleNameAsync (role: Role, normalizedName: string, cancellationToken: CancellationToken) : Task =
            role.NormalizedName <- normalizedName
            Task.CompletedTask

        member this.SetRoleNameAsync (role: Role, roleName: string, cancellationToken: CancellationToken) : Task =
            role.Name <- roleName
            Task.CompletedTask

        member this.UpdateAsync (role: Role, cancellationToken: CancellationToken) : Task<IdentityResult> = task {
            cancellationToken.ThrowIfCancellationRequested()
            use con = new MySqlConnection(connectionString)
            do! con.OpenAsync()
            let! _ = con.ExecuteAsync(@"UPDATE role SET Name = @Name, NormalizedName = @NormalizedName WHERE IDRole = @IDRole", role)
            return IdentityResult.Success
        }

        member this.Dispose() : unit = ()
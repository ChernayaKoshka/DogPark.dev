[<AutoOpen>]
module DogPark.Authentication.RoleStore

open Microsoft.AspNetCore.Identity
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2.ContextInsensitive
open DogPark

type MariaDBRoleStore(queries: Queries) =
    interface IRoleStore<Role> with
        member this.CreateAsync (role: Role, cancellationToken: CancellationToken) : Task<IdentityResult> = task {
            let! id = queries.RoleCreate(role, cancellationToken)
            role.IDRole <- id
            return IdentityResult.Success
        }

        member this.DeleteAsync (role: Role, cancellationToken: CancellationToken) : Task<IdentityResult> = task {
            do! queries.RoleDelete(role, cancellationToken)
            return IdentityResult.Success
        }

        member this.FindByIdAsync (roleId: string, cancellationToken: CancellationToken) : Task<Role> =
            queries.RoleFindById (roleId, cancellationToken)

        member this.FindByNameAsync (normalizedRoleName: string, cancellationToken: CancellationToken) : Task<Role> =
            queries.RoleFindByName(normalizedRoleName, cancellationToken)

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
            do! queries.RoleUpdate(role, cancellationToken)
            return IdentityResult.Success
        }

        member this.Dispose() : unit = ()
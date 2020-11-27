namespace DogPark

open Dapper
open System.Collections.Generic
open System.Linq
open FSharp.Control.Tasks.V2.ContextInsensitive
open MySql.Data.MySqlClient
open System.Threading

type Queries(connectionString) =
    let connectionString: string = connectionString

    let makeOpenConnection() = task {
        let connection = new MySqlConnection(connectionString)
        do! connection.OpenAsync()
        return connection
    }

    member __.GetArticleById (idArticle: int) = task {
        use! connection = makeOpenConnection()
        return! connection.QuerySingleAsync<Article>(
            """
            SELECT * FROM article
            WHERE IDArticle = @IDArticle
            """,
            {| IDArticle = idArticle |}
        )
    }

    // BEGIN ROLESTORE
    member __.RoleCreate (role: Role, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        return!
            connection.QuerySingleAsync<int>(
                """
                INSERT INTO role (Name, NormalizedName)
                VALUES (@Name, @NormalizedName);
                SELECT CAST(last_insert_id() as int)
                """,
                role
        )
    }

    member __.RoleDelete (role: Role, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        let! _ =
            connection.ExecuteAsync(
                """
                DELETE FROM role
                WHERE IDRole = @IDRole
                """,
                role
            )
        return ()
    }

    member __.RoleFindById (roleId: string, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        return!
            connection.QuerySingleOrDefaultAsync<Role>(
                """
                SELECT * FROM role
                WHERE IDRole = @IDRole
                """,
                {| IDRole = int roleId |}
        )
    }

    member __.RoleFindByName (normalizedRoleName: string, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        return!
            connection.QuerySingleOrDefaultAsync<Role>(
                """
                SELECT * FROM role
                WHERE NormalizedName = @NormalizedName
                """,
                {| NormalizedName = normalizedRoleName |}
            )
    }

    member __.RoleUpdate (role: Role, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        let! _ =
            connection.ExecuteAsync(
                """
                UPDATE role
                SET Name = @Name,
                    NormalizedName = @NormalizedName
                WHERE IDRole = @IDRole
                """,
                role
            )
        return ()
    }

    // END ROLESTORE
    // BEGIN USERSTORE

    member __.UserCreate(user: User, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        return!
            connection.QuerySingleAsync<int>(
                """
                INSERT INTO user (UserName, NormalizedUserName, PasswordHash)
                VALUES (@UserName, @NormalizedUserName, @PasswordHash);
                SELECT CAST(last_insert_id() as int)
                """,
                user
            )
    }

    member __.UserDelete(user: User, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        let! _ =
            connection.QuerySingleAsync<int>(
                """
                DELETE FROM user
                WHERE IDUser = @IDUser
                """,
                user
            )
        return ()
    }

    member __.UserFindById(idUser: int, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        return!
            connection.QuerySingleOrDefaultAsync<User>(
                """
                SELECT * FROM user
                WHERE IDUser = @IDUser
                """,
                {| IDUser = idUser |}
            )
    }

    member __.UserFindByName(normalizedUserName: string, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        return!
            connection.QuerySingleOrDefaultAsync<User>(
                """
                SELECT * FROM user
                WHERE NormalizedUserName = @NormalizedUserName
                """,
                {| NormalizedUserName = normalizedUserName |}
            )
    }

    member __.UserUpdate(user: User, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        let! _ =
            connection.ExecuteAsync(
                """
                UPDATE user
                SET UserName = @UserName,
                    PasswordHash = @PasswordHash
                WHERE IDUser = @IDUser
                """,
                user
            )
        return ()
    }

    member __.UserAddToRole(user: User, normalizedRoleName: string, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        let! _ =
            connection.ExecuteAsync(
                """
                INSERT INTO userrole(IDUser, IDRole)
                SELECT @IDUser, IDRole FROM role
                WHERE NormalizedName = @RoleName
                """,
                {| IDUser = user.IDUser; RoleName = normalizedRoleName |}
            )
        return ()
    }

    member __.UserGetRoles(user: User, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        let! result =
            connection.QueryAsync<string>(
                """
                SELECT Name FROM role
                JOIN userrole ur ON ur.IDRole = role.IDRole
                WHERE ur.IDUser = @IDUser
                """,
                user
            )
        return result.ToList() :> IList<string>
    }

    member __.UserGetInRole(normalizedRoleName: string, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        let! result =
            connection.QueryAsync<User>(
                """
                SELECT user.* FROM user user
                JOIN userrole ur ON ur.IDUser = user.IDUser
                JOIN role r ON ur.IDRole = r.IDRole
                WHERE r.Name = @RoleName
                """,
                {| RoleName = normalizedRoleName |})
        return result.ToList() :> IList<User>
    }

    member __.UserIsInRole(user: User, normalizedRoleName: string, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        return!
            connection.QuerySingleAsync<bool>(
                """
                SELECT COUNT(*) > 0 FROM user user
                JOIN userrole ur ON ur.IDUser = user.IDUser
                JOIN role r ON ur.IDRole = r.IDRole
                WHERE r.Name = @RoleName
                """,
                {| RoleName = normalizedRoleName |})
    }

    member __.UserRemoveFromeRole(user: User, normalizedRoleName: string, ?cancellationToken: CancellationToken) = task {
        let cancellationToken = Option.defaultValue CancellationToken.None cancellationToken
        cancellationToken.ThrowIfCancellationRequested()
        use! connection = makeOpenConnection()
        let! _ =
            connection.QuerySingleAsync<bool>(
                """
                DELETE ur FROM userrole ur
                INNER JOIN role r ON r.IDRole = ur.IDRole
                WHERE ur.IDUser = @IDUser
                AND r.NormalizedName = @RoleName
                AND ur.IDRole = r.IDRole
                """,
                {| IDUser = user.IDUser; RoleName = normalizedRoleName |})
        return ()
    }

    // END USERSTORE
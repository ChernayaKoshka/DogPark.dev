[<AutoOpen>]
module DogPark.Authentication.Types

type User() =
    member val User = -1 with get, set 
    member val UserName : string = null with get, set 
    member val NormalizedUserName : string = null with get, set 
    member val PasswordHash : string = null with get, set

type Role() =
    member val Role = -1 with get, set
    member val Name = "ERROR" with get, set
    member val NormalizedName = "ERROR" with get, set
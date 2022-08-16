namespace DogPark.HttpHandlers.Api
open DogPark.Shared
open Giraffe
open System
open System.IO
open FSharp.Control.Tasks
open DogPark

module Smorpa =
    let smorpa = 
        File.ReadAllLines(Path.Combine(webRoot, "pokemon/pokemon.txt"))
        |> Array.map (fun name ->
            (name, 
                Directory.GetFiles(Path.Combine(webRoot, "pokemon", name)) 
                |> Array.map (fun path ->
                    let filename = Path.GetFileName path
                    Path.Combine("pokemon", name, filename)
                )
            )
        )

    let random = new Random()

    let getSmorpa (id: int): HttpHandler = 
        fun next ctx -> task {
            let id = if id > smorpa.Length - 1 then 0 else id

            let (name, files) = smorpa[id]

            return! json
                {
                    Id = id
                    Name = name
                    ImageUrls = files
                } next ctx
        }
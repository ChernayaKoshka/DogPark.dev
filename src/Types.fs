[<AutoOpen>]
module DogPark.Types

[<CLIMutable>]
type DBArticle =
    {
        Article : uint32
        Author : string
        Headline : string
        FilePath : string
    }

[<CLIMutable>]
type Article =
    {
        Author : string
        Headline : string
        Body : string
    }
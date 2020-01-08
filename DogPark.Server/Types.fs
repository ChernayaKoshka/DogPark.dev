[<AutoOpen>]
module DogPark.Types

[<CLIMutable>]
type LoginModel =
    {
        UserName : string
        Password : string
    }

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

[<CLIMutable>]
type ShortenUrlPostData =
    {
        LongUrl : string
    }
[<AutoOpen>]
module DogPark.Types

type Article =
    {
        IDArticle: int
        IDUser: int
        Headline: string
        FilePath: string
    }

type ShortUrl =
    {
        IDShortUrl: int
        IDUser: int
        Short: string
        Long: string
    }
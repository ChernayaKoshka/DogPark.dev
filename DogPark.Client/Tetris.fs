module DogPark.Tetris.JS

open Fable.Core.JsInterop
open Browser.Types
open Browser
open System
open Fable.Core

// Standard Tetris board is 10w x 20h
let boardWidth = 30.
let boardHeight = 5.
let blockDimension = 20. // 20x20 pixels

// to canvas dimensions
let canvasWidth = boardWidth * blockDimension + 1.
let canvasHeight = boardHeight * blockDimension + 1.

let canvas = document.getElementById "tetris" :?> HTMLCanvasElement
canvas.width <- canvasWidth
canvas.height <- canvasHeight

let context = canvas.getContext_2d()


type Point =
    {
        X: int
        Y: int
    }
    with override this.ToString() = sprintf "(%d,%d)" this.X this.Y


type Block = Point
type Tetromino =
    {
        Origin: Point
        Pivot: Point

        // used to "correct" positioning to match the SRS
        Offsets: Point[]
        OffsetIndex: int

        Blocks: Block[]
    }
    with
        override this.ToString() =
            this.Blocks
            |> Array.map string
            |> String.concat ","

type Direction =
    | Left
    | Right
    | Down

let directionAsVector direction =
    match direction with
    | Left -> { X = -1; Y = 0 }
    | Right -> { X = 1; Y = 0 }
    | Down -> { X = 0; Y = 1 }

let IOffsets = [| { X = 0; Y = 0; }; { X = 0; Y = 0; }; { X = 0; Y = 0; }; { X = 0; Y = 0  }; |]
let IPiece =
    {
        Origin = { X = 0; Y = 0 }
        Pivot = { X = 2; Y = 2 }
        Offsets = IOffsets
        OffsetIndex = 0
        Blocks =
            [|
                { X = 1; Y = 2; }; { X = 2; Y = 2; }; { X = 3; Y = 2; }; { X = 4; Y = 2 }
            |]
    }

let JLSTZOffsets = [| for _ = 0 to 3 do yield { X = 0; Y = 0 } |]

let JPiece =
    {
        Origin = { X = 0; Y = 0 }
        Pivot = { X = 1; Y = 1 }
        Offsets = JLSTZOffsets
        OffsetIndex = 0
        Blocks =
            [|
                { X = 0; Y = 0; };
                { X = 0; Y = 1; }; { X = 1; Y = 1; }; { X = 2; Y = 1 }
            |]
    }

let LPiece =
    {
        Origin = { X = 0; Y = 0 }
        Pivot = { X = 1; Y = 1 }
        Offsets = JLSTZOffsets
        OffsetIndex = 0
        Blocks =
            [|
                                                      { X = 2; Y = 0; };
                { X = 0; Y = 1; }; { X = 1; Y = 1; }; { X = 2; Y = 1; }
            |]
    }

let OOffsets = [| { X = 0; Y = 0; }; { X = -1; Y = 0  }; { X = -1; Y = -1; }; { X = 0; Y = -1 }; |]
let OPiece =
    {
        Origin = { X = 0; Y = 0 }
        Pivot = { X = 1; Y = 1; }
        Offsets = OOffsets
        OffsetIndex = 0
        Blocks =
            [|
                { X = 0; Y = 0; }; { X = 1; Y = 0; }
                { X = 0; Y = 1; }; { X = 1; Y = 1; }
            |]
    }

let SPiece =
    {
        Origin = { X = 0; Y = 0 }
        Pivot = { X = 1; Y = 1 }
        Offsets = JLSTZOffsets
        OffsetIndex = 0
        Blocks =
            [|
                                   { X = 1; Y = 0; }; { X = 2; Y = 0; };
                { X = 0; Y = 1; }; { X = 1; Y = 1; };
            |]
    }

let TPiece =
    {
        Origin = { X = 0; Y = 0 }
        Pivot = { X = 1; Y = 1 }
        Offsets = JLSTZOffsets
        OffsetIndex = 0
        Blocks =
            [|
                                   { X = 1; Y = 0; };
                { X = 0; Y = 1; }; { X = 1; Y = 1; }; { X = 2; Y = 1; };
            |]
    }

let ZPiece =
    {
        Origin = { X = 0; Y = 0 }
        Pivot = { X = 1; Y = 1 }
        Offsets = JLSTZOffsets
        OffsetIndex = 0
        Blocks =
            [|
                { X = 0; Y = 0; }; { X = 1; Y = 0; };
                                   { X = 1; Y = 1; }; { X = 2; Y = 1; };
            |]
    }


let placeAtPoint (point: Point) (piece: Tetromino) =
    { piece with Origin = point }

let move direction (point: Block) =
    let vector = directionAsVector direction
    { point with
        X = point.X + vector.X
        Y = point.Y + vector.Y
    }

let movePiece direction (piece: Tetromino) =
    { piece with
        Origin = move direction piece.Origin
    }

// https://math.stackexchange.com/a/1330166/303550
let rotatePoint90DegreesCW (point: Point) = { X = point.Y; Y = -point.X; }
let rotatePoint90Degrees (point: Point) = { X = -point.Y; Y = point.X; }
let rotatePiece (piece: Tetromino) =
    let newOffsetIndex =
        if piece.OffsetIndex + 1 = 4 then 0
        else piece.OffsetIndex + 1
    { piece with
        OffsetIndex = newOffsetIndex
        Blocks =
            piece.Blocks
            |> Array.map (fun block ->
                let offset =
                    {
                        // sign is + here to invert
                        X = block.X - piece.Pivot.X - piece.Offsets.[piece.OffsetIndex].X
                        Y = block.Y - piece.Pivot.Y - piece.Offsets.[piece.OffsetIndex].Y
                    }
                let rotated = rotatePoint90Degrees offset
                {
                    X = rotated.X + piece.Pivot.X + piece.Offsets.[newOffsetIndex].X
                    Y = rotated.Y + piece.Pivot.Y + piece.Offsets.[newOffsetIndex].Y
                })
    }

let drawLine color x y x' y' =
    context.strokeStyle <- color
    context.beginPath()
    context.moveTo(x, y)
    context.lineTo(x', y')
    context.stroke()

let blackLine = drawLine !^"black"

let drawBlock style block =
    context.fillStyle <- style
    context.fillRect(
        ((float block.X) * blockDimension + 1.),
        ((float block.Y) * blockDimension + 1.),
        (blockDimension - 1.),
        (blockDimension - 1.))

let drawPiece style (piece: Tetromino) =
    piece.Blocks
    |> Array.map (fun block ->
        {
            X = block.X + piece.Origin.X
            Y = block.Y + piece.Origin.Y
        })
    |> Array.iter (drawBlock style)

    let tx = (float piece.Origin.X + float piece.Pivot.X) * blockDimension
    let ty = (float piece.Origin.Y + float piece.Pivot.Y) * blockDimension
    drawLine !^"green" tx ty (tx + 1.) (ty + 1.)


let drawGridLines() =
    context.strokeStyle <- !^"black"
    for x = 0 to int boardWidth do
        let xPos = (float x * blockDimension) + 0.5
        blackLine xPos 0. xPos canvasHeight
    for y = 0 to int boardHeight do
        let yPos = (float y * blockDimension) + 0.5
        blackLine 0. yPos canvasWidth yPos

drawGridLines()

let iToC (piece: Tetromino) : U3<string, CanvasGradient, CanvasPattern> =
    match piece.OffsetIndex with
    | 0 -> !^"red"
    | 1 -> !^"green"
    | 2 -> !^"blue"
    | 3 -> !^"yellow"

let rec test pieces () =
    // drawPiece (iToC piece) piece
    // let next = movePiece Down piece
    pieces
    |> Array.iter (fun piece -> drawPiece (iToC piece) piece)

    let next =
        pieces
        |> Array.map rotatePiece

    window.setTimeout(test next, TimeSpan.FromSeconds(1.).TotalMilliseconds |> int)

let pieces =
    [|
        yield placeAtPoint { X =  0; Y = 0; } IPiece
        yield placeAtPoint { X =  6; Y = 0; } JPiece
        yield placeAtPoint { X = 10; Y = 0; } LPiece
        yield placeAtPoint { X = 14; Y = 0; } OPiece
        yield placeAtPoint { X = 18; Y = 0; } SPiece
        yield placeAtPoint { X = 22; Y = 0; } TPiece
        yield placeAtPoint { X = 26; Y = 0; } ZPiece
    |]

test pieces ()
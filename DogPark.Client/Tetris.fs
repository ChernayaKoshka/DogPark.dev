module DogPark.Tetris.JS

open Fable.Core.JsInterop
open Browser.Types
open Browser
open System
open Fable.Core

// Standard Tetris board is 10w x 20h
let boardWidth = 3.
let boardHeight = 3.
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

let JLSTZOffsets = [| for _ = 0 to 3 do yield { X = 0; Y = 0 } |]
let IOffsets = [| { X = 0; Y = 0; }; { X = -1; Y = 0; }; { X = -1; Y = 1;  }; { X = 0; Y = 1  }; |]
//let OOffsets = [| { X = -2; Y = 0; }; { X = 0; Y = -3  }; { X = 3; Y = -1; }; { X = 1; Y = 2 }; |]
let OOffsets = [| { X = 0; Y = 0; }; { X = 1; Y = 0  }; { X = 1; Y = -1; }; { X = 0; Y = -1 }; |]
let OPiece =
    {
        Origin = { X = 0; Y = 0 }
        Pivot = { X = 1; Y = 1; }
        Offsets = OOffsets
        OffsetIndex = 0
        Blocks =
            [|
                { X = 1; Y = 0; }; { X = 2; Y = 0; }
                { X = 1; Y = 1; }; { X = 2; Y = 1; }
            |]
    }

(*
        X
        X
       XX

       X
       XXX
*)

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
                let rotated = rotatePoint90DegreesCW offset
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

(*
    TODO: FIGURE OUT STUPID FUCKING INVERSE Y AXIS SHIT. TRANSLATE CARTESIAN TO SCREEN COORDS
*)

let drawPiece style (piece: Tetromino) =
    piece.Blocks
    |> Array.map (fun block ->
        {
            X = block.X + piece.Origin.X
            Y = block.Y + piece.Origin.Y
        })
    |> Array.iter (drawBlock style)
    // context.fillStyle <- !^"black"
    // context.font <- "30px Arial"
    // context.fillText(
    //     sprintf "%d: (%d, %d)" piece.OffsetIndex piece.Offsets.[piece.OffsetIndex].X piece.Offsets.[piece.OffsetIndex].Y,
    //     ((float piece.Blocks.[0].X + float piece.Origin.X) * blockDimension + 1.),
    //     ((float piece.Blocks.[0].Y + float piece.Origin.Y) * blockDimension + 1. + blockDimension + 1.))
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

let rec test piece () =
    // drawPiece (iToC piece) piece
    // let next = movePiece Down piece
    drawPiece (iToC piece) piece
    window.setTimeout(test (rotatePiece piece), TimeSpan.FromSeconds(1.).TotalMilliseconds |> int)

test (placeAtPoint { X = 0; Y = 0; } OPiece) ()
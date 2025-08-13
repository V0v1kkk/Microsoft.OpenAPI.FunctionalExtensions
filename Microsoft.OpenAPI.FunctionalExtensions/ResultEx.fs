module ResultEx

// Helpers over FSharp.Core's Result<'ok,'error>

let map f r =
  match r with
  | Ok v -> Ok (f v)
  | Error e -> Error e

let bind f r =
  match r with
  | Ok v -> f v
  | Error e -> Error e

let mapError f r =
  match r with
  | Ok v -> Ok v
  | Error e -> Error (f e)

let bindError f r =
  match r with
  | Ok v -> Ok v
  | Error e -> f e

let apply fR xR =
  match fR, xR with
  | Ok f, Ok x -> Ok (f x)
  | Error e, Ok _ -> Error e
  | Ok _, Error e -> Error e
  | Error e1, Error _ -> Error e1

let ofOption err = function
  | Some v -> Ok v
  | None -> Error err

let toOption = function
  | Ok v -> Some v
  | Error _ -> None

let ofChoice = function
  | Choice1Of2 v -> Ok v
  | Choice2Of2 e -> Error e

let toChoice = function
  | Ok v -> Choice1Of2 v
  | Error e -> Choice2Of2 e

let tee f x = f x; x

let teeError f = function
  | Ok v -> Ok v
  | Error e -> f e; Error e

let tryCatch (f: 'a -> 'b) (mapEx: exn -> 'e) (x: 'a) : Result<'b,'e> =
  try Ok (f x) with ex -> Error (mapEx ex)

let sequence (results: seq<Result<'a,'e>>) : Result<seq<'a>,'e> =
  let folder acc next =
    match acc, next with
    | Ok xs, Ok x -> Ok (Seq.append xs (Seq.singleton x))
    | Error e, _ -> Error e
    | _, Error e -> Error e
  results |> Seq.fold folder (Ok Seq.empty)

let traverse (f: 'a -> Result<'b,'e>) (items: seq<'a>) : Result<seq<'b>,'e> =
  items |> Seq.map f |> sequence

module AsyncResult =
  let ofAsync (a: Async<'a>) : Async<Result<'a,'e>> = async {
    let! v = a
    return Ok v
  }

  let map f (ar: Async<Result<'a,'e>>) : Async<Result<'b,'e>> = async {
    let! r = ar
    return map f r
  }

  let bind (f: 'a -> Async<Result<'b,'e>>) (ar: Async<Result<'a,'e>>) : Async<Result<'b,'e>> = async {
    let! r = ar
    match r with
    | Ok v -> return! f v
    | Error e -> return Error e
  }

  let mapError f (ar: Async<Result<'a,'e>>) : Async<Result<'a,'f>> = async {
    let! r = ar
    return mapError f r
  }

  let ofResult r = async { return r }
  let toResult (ar: Async<Result<'a,'e>>) = ar



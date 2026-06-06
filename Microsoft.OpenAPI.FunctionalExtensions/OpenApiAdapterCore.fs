[<RequireQualifiedAccess>]
module Microsoft.OpenAPI.FunctionalExtensions.AdapterCore

open System
open System.Collections.Generic

/// Converts a null reference to `None`; otherwise `Some` value.
let ofObj (value: 'T) : 'T option =
  if isNull (box value) then None else Some value

/// Converts `Nullable<T>` with no value to `None`; otherwise `Some` value.
let ofNullable (value: Nullable<'T>) : 'T option =
  if value.HasValue then Some value.Value else None

/// Reads a dictionary into an immutable map, skipping null values.
let readMap (dictionary: IDictionary<'k, 'v>) : Map<'k, 'v> =
  match dictionary with
  | null -> Map.empty
  | dict ->
      dict
      |> Seq.choose (fun entry ->
          if isNull (box entry.Value) then None else Some (entry.Key, entry.Value))
      |> Map.ofSeq

/// Reads a sequence into a list, skipping null elements.
let readSeq (sequence: #seq<'T>) : 'T list =
  match sequence with
  | null -> []
  | seq ->
      seq
      |> Seq.choose (fun element ->
          if isNull (box element) then None else Some element)
      |> Seq.toList

/// Reads a set into an immutable F# set.
let readSet (set: ISet<'T>) : Set<'T> =
  match set with
  | null -> Set.empty
  | s ->
      s
      |> Seq.filter (fun element -> not (isNull (box element)))
      |> Set.ofSeq

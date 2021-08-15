module SeqExtensions

open System
open System.Collections.Generic
open System.Text
open System.Linq

/// Join a sequence of strings using a delimiter.
/// Equivalent to String.Join() but without arrays.
let join (items : seq<string>) (delim : string) =
    // Collect the result in the string builder buffer
    // The end-sequence will be "item1,delim,...itemN,delim"
    let buff = 
        Seq.fold 
            (fun (buff :StringBuilder) (s:string) -> buff.Append(s).Append(delim)) 
            (new StringBuilder()) 
            items

    // We don't want the last delim in the result buffer, remove
    buff.Remove(buff.Length-delim.Length, delim.Length).ToString()

/// Convert a seq to a dictionary with key and value selectors.
let toDictionary (f:'s -> 'k) (g:'s -> 'v) (xs:_ seq) =
    xs.ToDictionary(f, g)

/// Join a seq of lines by Environment.NewLine.
let joinAsLines lines = join lines Environment.NewLine

/// Converts nullable generic list to seq safety. 
let toSeqSafe (list: List<'a>) =
  match list with
  | null -> Seq.empty
  | _    -> seq list
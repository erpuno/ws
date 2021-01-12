namespace N2O

open System
open System.Collections.Specialized

module Req =

    let parse (lines : string array) : Req =
        match (Array.head lines).Split(' ', StringSplitOptions.RemoveEmptyEntries) with
        | [| method; uri; version |] ->
            let headers = NameValueCollection()
            Array.iteri (fun idx (line : string) ->
                if idx > 0 then
                    match line.Split(':', 2, StringSplitOptions.TrimEntries) with
                    | [| key; value |] -> headers.Add(key.ToLower(), value)
                    | _ -> ArgumentException (sprintf "Invalid Header: %s" line) |> raise
                else ()) lines

            { path = uri; version = version; method = method;
              headers = headers }
        | _ -> raise (ArgumentException "Invalid Request-Line")

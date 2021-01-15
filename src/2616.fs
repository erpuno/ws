namespace N2O

open System
open System.Collections.Specialized

// RFC 2616 HTTP headers

module Req =

    let parse (lines : string array) : Req =
        match (Array.head lines).Split(' ', StringSplitOptions.RemoveEmptyEntries) with
        | [| method; uri; version |] ->
            let headers = NameValueCollection()
            Array.iteri (fun idx (line : string) ->
                if idx > 0 then
                    match line.Split(':', 2, StringSplitOptions.TrimEntries) with
                    | [| key; value |] -> headers.Add(key.ToLower(), value)
                    | _ -> ()
                else ()) lines
            { path = uri; version = version; method = method; headers = headers }
        | _ ->
            { path = ""; version = ""; method = ""; headers = NameValueCollection() }

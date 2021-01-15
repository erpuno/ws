namespace N2O

open System
open System.Collections.Specialized

// RFC 2616 HTTP headers

[<AutoOpen>]
module RFC2616 =

    let ignoreHead (act : 'a -> unit) : int -> 'a -> unit =
        fun idx x -> if idx > 0 then act x else ()

    let parseHeader (headers : NameValueCollection) (line : string) : unit =
        match line.Split(':', 2, StringSplitOptions.TrimEntries) with
        | [| key; value |] -> headers.Add(key.ToLower(), value)
        | _ -> ()

    let request (lines : string array) : Req =
        let req = { path = ""; version = ""; method = ""; headers = NameValueCollection() }

        match (Array.head lines).Split(' ', StringSplitOptions.RemoveEmptyEntries) with
        | [| method; uri; version |] ->
            Array.iteri (ignoreHead <| parseHeader req.headers) lines
            { req with path = uri; version = version; method = method }
        | _ -> req

namespace N2O

open System
open System.Collections.Specialized

// RFC 2616 HTTP headers

[<AutoOpen>]
module RFC2616 =

    let parseHeader (headers : NameValueCollection) (line : string) : unit =
        match line.Split([| ':' |], 2) with
        | [| key; value |] -> headers.Add(key.ToLower().Trim(), value.Trim())
        | _ -> ()

    let request (lines : string array) : Req =
        let req = { path = ""; version = ""; method = ""; headers = NameValueCollection() }

        match (Array.head lines).Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries) with
        | [| method; uri; version |] ->
            Array.iter (parseHeader req.headers) (Array.tail lines)
            { req with path = uri; version = version; method = method }
        | _ -> req

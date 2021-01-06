namespace N2O

open System
open System.IO
open System.Text
open System.Security.Cryptography

[<AutoOpen>]
module RFC6455 =

    let guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
    let endOfLine = "\r\n"
    let line txt = txt + endOfLine

    let getKey (key: String) arr =
        try
            let f (s: String) = s.StartsWith(key)
            (Array.find f arr).[key.Length + 1..]
        with _ -> ""

    let isWebSocketsUpgrade (lines: string array) =
        [| "GET /n2o HTTP/1.1"
           "Upgrade: websocket"
           "Connection: Upgrade" |]
        |> Array.map
            (fun x ->
                lines

                |> Array.exists (fun y -> x.ToLower() = y.ToLower()))
        |> Array.reduce (fun x y -> x && y)

    let getLines (bytes: Byte []) len =
        if len > 8 then
            bytes.[..(len - 9)]
            |> UTF8Encoding.UTF8.GetString
            |> fun hs -> hs.Split([| endOfLine |], StringSplitOptions.RemoveEmptyEntries)
        else
            [||]

    let calcWSAccept6455 (secWebSocketKey: string) =
        let sha1 = SHA1CryptoServiceProvider.Create()

        secWebSocketKey + guid
        |> Encoding.ASCII.GetBytes
        |> sha1.ComputeHash
        |> Convert.ToBase64String

    let createAcceptString6455 acceptCode =
        line "HTTP/1.1 101 Switching Protocols"
        + line "Upgrade: websocket"
        + line "Connection: Upgrade"
        + line ("Sec-WebSocket-Accept: " + acceptCode)
        + line ""

    let wsResponse lines =
        (match lines with
         | [||] -> ""
         | _ ->
             getKey "Sec-WebSocket-Key:" lines
             |> calcWSAccept6455
             |> createAcceptString6455)
        |> Encoding.ASCII.GetBytes

    let webSocket (lines: string array) =
        (isWebSocketsUpgrade lines, wsResponse lines)

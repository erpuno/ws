namespace N2O

open System
open System.IO
open System.Text
open System.Security.Cryptography

[<AutoOpen>]
module RFC6455 =

    let isWebSocketsUpgrade (lines: string array) =
        Array.exists (fun (x:string) -> "upgrade: websocket" = x.ToLower()) lines

    let getKey (key: String) arr =
        try let f (s: String) = s.StartsWith(key)
            (Array.find f arr).[key.Length + 1..]
        with _ -> ""

    let getLines (bytes: Byte []) len =
        if len > 8 then
            bytes.[..(len - 9)]
            |> UTF8Encoding.UTF8.GetString
            |> fun hs -> hs.Split([| "\r\n" |], StringSplitOptions.RemoveEmptyEntries)
        else
            [||]

    let acceptString6455 acceptCode =
        "HTTP/1.1 101 Switching Protocols\r\n" +
        "Upgrade: websocket\r\n" +
        "Connection: Upgrade\r\n" +
        "Sec-WebSocket-Accept: " + acceptCode + "\r\n\r\n"

    let handshake lines =
        (getKey "Sec-WebSocket-Key:" lines) + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        |> Encoding.ASCII.GetBytes
        |> SHA1CryptoServiceProvider.Create().ComputeHash
        |> Convert.ToBase64String
        |> acceptString6455
        |> Encoding.ASCII.GetBytes

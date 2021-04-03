namespace N2O

open System
open System.IO
open System.Text
open System.Security.Cryptography

// RFC 6455 WebSocket handshake

[<AutoOpen>]
module RFC6455 =

    let isWebSocketsUpgrade (req: Req) =
        req.headers.["upgrade"].ToLower() = "websocket"

    let getLines (bytes: Byte []) len =
        if len > 8 then
            bytes.[..(len)]
            |> UTF8Encoding.UTF8.GetString
            |> fun hs -> hs.Split([| "\r\n" |], StringSplitOptions.RemoveEmptyEntries)
        else
            [||]

    let acceptString6455 acceptCode =
        "HTTP/1.1 101 Switching Protocols\r\n" +
        "Upgrade: websocket\r\n" +
        "Connection: Upgrade\r\n" +
        "Sec-WebSocket-Accept: " + acceptCode + "\r\n\r\n"

    let handshake (req: Req) =
        req.headers.["sec-websocket-key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        |> Encoding.ASCII.GetBytes
        |> SHA1CryptoServiceProvider.Create().ComputeHash
        |> Convert.ToBase64String
        |> acceptString6455
        |> Encoding.ASCII.GetBytes

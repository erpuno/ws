namespace N2O

open System
open System.IO
open System.Text
open System.Security.Cryptography

// RFC 6455 WebSocket handshake

[<AutoOpen>]
module RFC6455 =

    type WebsocketFrame =
        | ContinuationFrame  //%x0
        | TextFrame          //%x1
        | BinaryFrame        //%x2
        | ReservedNonControl //%x3-7
        | ConnectionClosed   //%x8
        | Ping               //%x9
        | Pong               //%xA
        | ReservedControl    //%xB-F

    let isWebSocketsUpgrade (req : Req) =
        req.headers.["upgrade"].ToLower() = "websocket"

    let getLines (bytes : Byte []) len =
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

    let handshake (req : Req) =
        req.headers.["sec-websocket-key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
        |> Encoding.ASCII.GetBytes
        |> SHA1CryptoServiceProvider.Create().ComputeHash
        |> Convert.ToBase64String
        |> acceptString6455
        |> Encoding.ASCII.GetBytes

    let swapEndian (bs : byte array) = if BitConverter.IsLittleEndian then Array.rev bs else bs

    let decodeFrame (stream : Stream) =
        async {
            let! firstByte_t = stream.AsyncRead(1)
            let firstByte = firstByte_t.[0]
            let final = firstByte &&& 128uy <> 0uy
            let opcode =
                match firstByte &&& 15uy with
                | 0uy -> ContinuationFrame
                | 1uy -> TextFrame
                | 2uy -> BinaryFrame
                | 3uy | 4uy | 5uy | 6uy | 7uy -> ReservedNonControl
                | 8uy -> ConnectionClosed
                | 9uy -> Ping
                | 10uy -> Pong
                | 11uy | 12uy | 13uy | 14uy | 15uy -> ReservedControl
                | _ -> failwith "given the mask above this cannot happen"

            let! secondByte_t = stream.AsyncRead(1)
            let secondByte = secondByte_t.[0]
            let mask = secondByte &&& 128uy <> 0uy

            let! payloadLength =
                match secondByte &&& 127uy with
                | 127uy ->
                    async {
                        let! int64Bytes = stream.AsyncRead(8)
                        return BitConverter.ToUInt64(int64Bytes |> swapEndian, 0)
                    }
                | 126uy ->
                    async {
                        let! int16Bytes = stream.AsyncRead(2)
                        return uint64 (BitConverter.ToUInt16(int16Bytes |> swapEndian, 0))
                    }
                | x -> async { return uint64 (Convert.ToUInt16(x)) }

            let payloadLength' = int32 payloadLength

            let! data =
                if mask
                then
                    async {
                        let! mask = stream.AsyncRead(4)
                        let! data = stream.AsyncRead(payloadLength')
                        return data |> Array.mapi (fun i b -> b ^^^ mask.[i % 4])
                    }
                else
                    stream.AsyncRead(payloadLength')
            return (opcode, data, final)
        }

    let encodeFrame (opcode : WebsocketFrame, data : byte array, final : bool, mask : uint32 option) =
        let firstByte =
            match opcode with
            | ContinuationFrame -> 0uy
            | TextFrame -> 1uy
            | BinaryFrame -> 2uy
            | ReservedNonControl -> failwith "Use of reserved unsuported opcode \"ReservedNonControl\""
            | ConnectionClosed -> 8uy
            | Ping -> 9uy
            | Pong -> 10uy
            | ReservedControl -> failwith "Use of reserved unsuported opcode \"ReservedControl\""
            |> fun x -> if final then x  ||| 128uy else x

        let length = data |> Array.length // NOTE  Length cannot be > int32 but can be > int16
        let lengthBytes =
            if length < 126 then [Convert.ToByte(length)]
            else if length < int32 UInt16.MaxValue then 126uy :: (BitConverter.GetBytes(length) |> swapEndian |> Seq.skip 2 |> Seq.toList)
            else 127uy :: ([0uy;0uy;0uy;0uy] @ ((BitConverter.GetBytes(length) |> swapEndian |> Seq.toList)))
            |> function
            | (x::xs) as ys -> if mask.IsSome then (x ||| 128uy) :: xs else ys // Set the mask bit if one is available
            | _ -> failwith "Should not happen - should have at least one byte in the list"

        let maskedData =
            match mask with
            | None -> data
            | Some(m) ->
                let maskBits = BitConverter.GetBytes(m) |> swapEndian
                Array.append maskBits (data |> Array.mapi (fun i b -> b ^^^ maskBits.[i % 4]))
        Array.append (firstByte :: lengthBytes |> List.toArray) maskedData

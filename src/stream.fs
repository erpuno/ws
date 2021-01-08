namespace N2O

open System
open System.Text
open System.Threading
open System.Net.WebSockets

// MailboxProcessor-based Tick pusher and pure Async WebSocket looper

[<AutoOpen>]
module Stream =

    let mutable protocol: Msg -> Msg = fun x -> x

    let sendBytes (ws: WebSocket) ct bytes =
        ws.SendAsync(ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, ct)
        |> ignore

    let send ws ct (msg: Msg) =
        async {
            match msg with
            | Text text -> sendBytes ws ct (Encoding.UTF8.GetBytes text)
            | Bin arr -> sendBytes ws ct arr
            | Nope -> ()
        }

    let telemetry (ws: WebSocket) (inbox: MailboxProcessor<Msg>)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                while not ct.IsCancellationRequested do
                    let! _ = inbox.Receive()
                    do! send ws ct (Text "TICK")
            finally
                sup.Post(Disconnect <| inbox)

                ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "TELEMETRY", ct)
                |> ignore
        }

    let looper (ws: WebSocket) (bufferSize: int)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                let mutable bytes = Array.create bufferSize (byte 0)
                while not ct.IsCancellationRequested do
                    let! result =
                        ws.ReceiveAsync(ArraySegment<byte>(bytes), ct)
                        |> Async.AwaitTask

                    let recv = bytes.[0..result.Count - 1]

                    match (result.MessageType) with
                    | WebSocketMessageType.Text ->
                      do! protocol (Text (Encoding.UTF8.GetString recv))
                          |> send ws ct
                    | WebSocketMessageType.Binary ->
                      do! send ws ct (protocol (Bin recv))
                    | WebSocketMessageType.Close -> ()
                    | _ -> printfn "PROTOCOL VIOLATION"
            finally
                sup.Post(Close <| ws)

                ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "LOOPER", ct)
                |> ignore
        }


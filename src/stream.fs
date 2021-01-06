namespace N2O

open System
open System.Text
open System.Threading
open System.Net.WebSockets

// MailboxProcessor-based Tick pusher and pure Async WebSocket looper

[<AutoOpen>]
module Stream =

    let mutable protocol: byte [] -> byte [] = fun x -> x

    let send (ws: WebSocket) (ct: CancellationToken) (bytes: byte []) =
        async {
            ws.SendAsync(ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, ct)
            |> ignore
        }

    let telemetry (ws: WebSocket) (inbox: MailboxProcessor<Payload>)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                while not ct.IsCancellationRequested do
                    let! _ = inbox.Receive()
                    do! send ws ct ("TICK" |> Encoding.ASCII.GetBytes)
            finally
                sup.Post(Disconnect <| inbox)

                ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "PUSHER DIE", ct)
                |> ignore
        }

    let looper (ws: WebSocket) (bufferSize: int)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                let mutable bytes = Array.create bufferSize (byte 0)
                while not ct.IsCancellationRequested do
                    let! (result: WebSocketReceiveResult) =
                        ws.ReceiveAsync(ArraySegment<byte>(bytes), ct)
                        |> Async.AwaitTask

                    match (result.MessageType) with
                    | WebSocketMessageType.Text -> do! send ws ct (protocol bytes.[0..result.Count])
                    | WebSocketMessageType.Binary -> do! send ws ct (protocol bytes.[0..result.Count])
                    | WebSocketMessageType.Close -> ()
                    | _ -> printfn "PROTOCOL VIOLATION"
            finally
                sup.Post(Close <| ws)

                ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "LOOPER DIE", ct)
                |> ignore
        }


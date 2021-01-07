namespace N2O

open System
open System.Net.Sockets
open System.Text
open System.Threading

// MailboxProcessor-based Tick pusher and pure Async WebSocket looper

[<AutoOpen>]
module Stream =

    let mutable protocol: byte [] -> byte [] = fun x -> x

    let send (ns: NetworkStream) (data: byte []) =
        async { do! ns.AsyncWrite(encodeFrame (BinaryFrame, data, true, None)) }

    let telemetry (ns: NetworkStream) (inbox: MailboxProcessor<Payload>)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                while not ct.IsCancellationRequested do
                    let! _ = inbox.Receive()
                    do! send ns ("TICK" |> Encoding.ASCII.GetBytes)
            finally
                sup.Post(Disconnect <| inbox)

                ns.Close()
                |> ignore
        }

    let looper (ns: NetworkStream) (bufferSize: int)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                let mutable bytes = Array.create bufferSize (byte 0)
                while not ct.IsCancellationRequested do
                    match! decodeFrame(ns) with
                    | (TextFrame,   data, true) -> do! send ns (protocol data)
                    | (BinaryFrame, data, true) -> do! send ns (protocol data)
                    | _ -> ()
            finally
                sup.Post(Close <| ns)

                ns.Close()
                |> ignore
        }


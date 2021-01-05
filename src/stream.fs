namespace N2O

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Runtime.Serialization
open System.Security.Cryptography

[<AutoOpen>]
module Stream =

    let writeTime (ns: NetworkStream)
                  (time: Time)
                  =
        async {
            let json =
                System.Runtime.Serialization.Json.DataContractJsonSerializer
                    (typeof<Time>)

            let payload = new MemoryStream()
            json.WriteObject(payload, time)
            let df = makeFrame_ShortTxt <| payload.ToArray()
            do! ns.AsyncWrite(df, 0, df.Length)
        }

    let runTelemetry (ns: NetworkStream)
                     (inbox: MailboxProcessor<Time>)
                     (ct: CancellationToken)
                     (ctrl: MailboxProcessor<Msg>)
                     =
        async {
            try
                while not ct.IsCancellationRequested do
                    let! _ = inbox.Receive()
                    do! writeTime ns (Time.New(DateTime.Now))
            finally
                printfn "PUSHER DIE"
                ctrl.Post(Disconnect <| inbox)
                ns.Close()
        }

    let runLoop (ns: NetworkStream)
                (size: int)
                (inbox: MailboxProcessor<Time>)
                (ct: CancellationToken)
                (ctrl: MailboxProcessor<Msg>)
                =
        async {
            try
                while not ct.IsCancellationRequested do
                    let bytes = Array.create size (byte 0)

                    let ws =
                        WebSocket.CreateFromStream
                            ((ns :> Stream), true, "n2o", TimeSpan(1, 0, 0))

                    let! (result: WebSocketReceiveResult) =
                        ws.ReceiveAsync(ArraySegment<byte>(bytes), ct)
                        |> Async.AwaitTask

                    let len = result.Count

                    let _ =
                        match (int result.MessageType) with
                        | 2 ->
                            printfn "HANDLE CLOSE"
                        | 1 ->
                            printfn "HANDLE BINARY %A" bytes.[0..len]
                        | 0 ->
                            let text = BitConverter.ToString(bytes.[0..len])
                            printfn "HANDLE TEXT %s" text
                        | x ->
                            printfn "HANDLE %A" x

                    do! writeTime ns (Telemetry.Time.New(DateTime.Now))
            finally
                printfn "LOOP DIE"
                ctrl.Post(Disconnect <| inbox)
                ns.Close()
        }


    let heartbeat (interval: int)
                  (ct: CancellationToken)
                  (ctrl: MailboxProcessor<Msg>)
                  =
        async {
            while not ct.IsCancellationRequested do
                do! Async.Sleep interval
                ctrl.Post(Tick <| Time.New(DateTime.Now))
        }

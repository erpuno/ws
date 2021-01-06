namespace N2O

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Net.WebSockets
open System.Threading

// Pure MailboxProcessor-based WebSocket Server

[<AutoOpen>]
module Server =

    let startClient (tcp: TcpClient) (sup: MailboxProcessor<Sup>) (ct: CancellationToken) =
        MailboxProcessor.Start(
            (fun (inbox: MailboxProcessor<Payload>) ->
                async {
                    let ns = tcp.GetStream()
                    let size = tcp.ReceiveBufferSize
                    let bytes = Array.create size (byte 0)
                    let! len = ns.ReadAsync(bytes, 0, bytes.Length) |> Async.AwaitTask
                    let lines = getLines bytes len
                    match isWebSocketsUpgrade lines with
                    | true ->
                        do! ns.AsyncWrite (handshake lines)
                        let ws =
                            WebSocket.CreateFromStream(
                                (ns :> Stream), true, "n2o", TimeSpan(1, 0, 0))

                        sup.Post(Connect(inbox, ws))
                        Async.StartImmediate(telemetry ws inbox ct sup, ct)
                        return! looper ws size ct sup
                    | _ -> tcp.Close()
                }),
            cancellationToken = ct
        )

    let heartbeat (interval: int) (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            while not ct.IsCancellationRequested do
                do! Async.Sleep interval
                sup.Post(Tick)
        }

    let listen (listener: TcpListener) (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                while not ct.IsCancellationRequested do
                    let! client = listener.AcceptTcpClientAsync() |> Async.AwaitTask
                    client.NoDelay <- true
                    startClient client sup ct |> ignore
            finally
                listener.Stop()
        }

    let startSupervisor (ct: CancellationToken) =
        MailboxProcessor.Start(
            (fun (inbox: MailboxProcessor<Sup>) ->
                let listeners = ResizeArray<_>()

                async {
                    while not ct.IsCancellationRequested do
                        let! msg = inbox.Receive()

                        match msg with
                        | Close ws -> printfn "Close: %A" ws
                        | Connect (l, ns) ->
                            printfn "Connect: %A %A" l ns
                            listeners.Add(l)
                        | Disconnect l ->
                            Console.WriteLine "Disconnect"
                            listeners.Remove(l) |> ignore
                        | Tick -> listeners.ForEach(fun l -> l.Post Ping)
                }),
            cancellationToken = ct
        )

    let start (addr: string) (port: int) =
        let cts = new CancellationTokenSource()
        let token = cts.Token
        let sup = startSupervisor token
        let listener = TcpListener(IPAddress.Parse(addr), port)

        try
            listener.Start(10)
        with
        | :? SocketException -> failwithf "ERROR: %s/%i is using by another program" addr port
        | err -> failwithf "ERROR: %s" err.Message

        Async.StartImmediate(listen listener token sup, token)
        Async.StartImmediate(heartbeat 10000 token sup, token)

        { new IDisposable with
            member x.Dispose() = cts.Cancel() }

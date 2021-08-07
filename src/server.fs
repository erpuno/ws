namespace N2O

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading

// The 7 processes of pure MailboxProcessor-based WebSocket Server

[<AutoOpen>]
module Server =

    let mutable interval = 5000 // ticker interval
    let mutable ticker = true // enable server-initiated Tick messages
    let mutable proto : Req -> Msg -> Msg = fun _ _ -> Nope // choose protocol by Req

    let sendBytes (ns: NetworkStream) bytes =
        ns.AsyncWrite (encodeFrame (BinaryFrame, bytes, true, None))

    let sendMsg ns (msg: Msg) = async {
        match msg with
        | Text text -> do! sendBytes ns (Encoding.UTF8.GetBytes text)
        | Bin arr   -> do! sendBytes ns arr
        | Nope      -> ()
    }

    let telemetry (ns: NetworkStream) (inbox: MailboxProcessor<Msg>)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                while not ct.IsCancellationRequested do
                    let! _ = inbox.Receive()
                    do! sendMsg ns (Text "TICK")
            finally
                sup.Post(Disconnect <| inbox)
                ns.Close () |> ignore
        }

    let looper (ns: NetworkStream) (req: Req) (bufferSize: int)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                while not ct.IsCancellationRequested do
                    match! decodeFrame(ns) with
                    | (TextFrame, recv, true) ->
                        do! proto req (Text (Encoding.UTF8.GetString recv))
                            |> sendMsg ns
                    | (BinaryFrame, recv, true) ->
                        do! proto req (Bin recv)
                            |> sendMsg ns
                    | _ -> ()
            finally
                sup.Post(Close ns)
                ns.Close() |> ignore
        }

    let startClient (tcp: TcpClient) (sup: MailboxProcessor<Sup>) (ct: CancellationToken) =
        MailboxProcessor.Start(
            (fun (inbox: MailboxProcessor<Msg>) ->
                async {
                    let ns = tcp.GetStream()
                    let size = tcp.ReceiveBufferSize
                    let bytes = Array.create size (byte 0)
                    let! len = ns.ReadAsync(bytes, 0, bytes.Length) |> Async.AwaitTask

                    let cts = new CancellationTokenSource ()
                    ct.Register (fun () -> cts.Cancel ()) |> ignore
                    let token = cts.Token

                    try
                        let req = request <| getLines bytes len
                        if isWebSocketsUpgrade req then
                            do! ns.AsyncWrite <| handshake req
                            sup.Post(Connect(inbox, ns))

                            if ticker then Async.Start(telemetry ns inbox token sup, token)
                            return! looper ns req size token sup
                        else ()
                    finally
                        cts.Cancel ()
                        tcp.Close ()
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
            while not ct.IsCancellationRequested do
                let! client = listener.AcceptTcpClientAsync() |> Async.AwaitTask
                client.NoDelay <- true
                startClient client sup ct |> ignore
        }

    let startSupervisor (ct: CancellationToken) =
        MailboxProcessor.Start(
            (fun (inbox: MailboxProcessor<Sup>) ->
                let listeners = ResizeArray<_>()
                async {
                    while not ct.IsCancellationRequested do
                        match! inbox.Receive() with
                        | Close ws -> ()
                        | Connect (l, ns) -> listeners.Add(l)
                        | Disconnect l -> listeners.Remove(l) |> ignore
                        | Tick -> listeners.ForEach(fun l -> l.Post Nope)
                }),
            cancellationToken = ct
        )

    let start (addr: string) (port: int) =
        let cts = new CancellationTokenSource()
        let token = cts.Token
        let sup = startSupervisor token
        let listener = TcpListener(IPAddress.Parse(addr), port)

        try listener.Start(10) with
        | :? SocketException -> failwithf "%s:%i is acquired" addr port
        | err -> failwithf "%s" err.Message

        Async.Start(listen listener token sup, token)
        if ticker then Async.Start(heartbeat interval token sup, token)

        { new IDisposable with member x.Dispose() = cts.Cancel() }

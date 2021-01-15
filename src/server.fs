namespace N2O

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Net.WebSockets
open System.Text
open System.Threading

// The 7 processes of pure MailboxProcessor-based WebSocket Server

[<AutoOpen>]
module Server =

    let mutable interval = 5000 // ticker interval
    let mutable ticker = true // enable server-initiated Tick messages
    let mutable proto : Req -> Msg -> Msg = fun _ _ -> Nope // choose protocol by Req

    let sendBytes (ws: WebSocket) ct bytes =
        ws.SendAsync(ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, ct)
        |> Async.AwaitTask

    let sendMsg ws ct (msg: Msg) = async {
        match msg with
        | Text text -> do! sendBytes ws ct (Encoding.UTF8.GetBytes text)
        | Bin arr -> do! sendBytes ws ct arr
        | Nope -> ()
    }

    let telemetry (ws: WebSocket) (inbox: MailboxProcessor<Msg>)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                while not ct.IsCancellationRequested do
                    let! _ = inbox.Receive()
                    do! sendMsg ws ct (Text "TICK")
            finally
                sup.Post(Disconnect <| inbox)

                ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "TELEMETRY", ct)
                |> ignore
        }

    let looper (ws: WebSocket) (req: Req) (bufferSize: int)
        (ct: CancellationToken) (sup: MailboxProcessor<Sup>) =
        async {
            try
                let mutable bytes = Array.create bufferSize (byte 0)
                while not ct.IsCancellationRequested do
                    let! result =
                        ws.ReceiveAsync(ArraySegment<byte>(bytes), ct)
                        |> Async.AwaitTask

                    let recv = bytes.[0..result.Count - 1]

                    match result.MessageType with
                    | WebSocketMessageType.Text ->
                        do! proto req (Text (Encoding.UTF8.GetString recv))
                            |> sendMsg ws ct
                    | WebSocketMessageType.Binary ->
                        do! proto req (Bin recv)
                            |> sendMsg ws ct
                    | WebSocketMessageType.Close -> ()
                    | _ -> printfn "PROTOCOL VIOLATION"
            finally
                sup.Post(Close <| ws)

                ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "LOOPER", ct)
                |> ignore
        }

    let startClient (tcp: TcpClient) (sup: MailboxProcessor<Sup>) (ct: CancellationToken) =
        MailboxProcessor.Start(
            (fun (inbox: MailboxProcessor<Msg>) ->
                async {
                    let ns = tcp.GetStream()
                    let size = tcp.ReceiveBufferSize
                    let bytes = Array.create size (byte 0)
                    let! len = ns.ReadAsync(bytes, 0, bytes.Length) |> Async.AwaitTask

                    try
                        let req = request <| getLines bytes len
                        if isWebSocketsUpgrade req then
                            do! ns.AsyncWrite <| handshake req
                            let ws =
                                WebSocket.CreateFromStream(
                                    (ns :> Stream), true, "n2o", TimeSpan(1, 0, 0))
                            sup.Post(Connect(inbox, ws))
                            if ticker then Async.Start(telemetry ws inbox ct sup, ct)
                            return! looper ws req size ct sup
                        else ()
                    finally tcp.Close ()
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

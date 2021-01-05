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

// Pure MailboxProcessor-based WebSocket Server

[<AutoOpen>]
module Server =

    let port = 1900
    let ipAddress = IPAddress.Loopback.ToString()

    let startMailboxProcessor ct f =
        MailboxProcessor.Start(f, cancellationToken = ct)

    let runWorkers (tcp: TcpClient)
                   (ctrl: MailboxProcessor<Msg>)
                   (ct: CancellationToken)
                   =
        startMailboxProcessor ct (fun (inbox: MailboxProcessor<Time>) ->
            async {
                let ns = tcp.GetStream()
                let size = tcp.ReceiveBufferSize
                let bytes = Array.create size (byte 0)
                let! len =
                    ns.ReadAsync(bytes, 0, bytes.Length)
                    |> Async.AwaitTask

                match webSocket (getLines bytes len) with
                | (true, upgrade) ->
                    do! ns.AsyncWrite upgrade
                    ctrl.Post(Connect(inbox, ns))
                    Async.Start(runTelemetry ns inbox ct ctrl, ct)
                    Async.Start(runLoop ns size inbox ct ctrl, ct)
                | _ -> tcp.Close()
            })

    let acceptLoop (listener: TcpListener)
                   (ct: CancellationToken)
                   (ctrl: MailboxProcessor<Msg>)
                   =
        async {
            try
                let! client =
                     Async.FromBeginEnd
                         (listener.BeginAcceptTcpClient,
                         listener.EndAcceptTcpClient)

                client.NoDelay <- true
                runWorkers client ctrl ct |> ignore
            finally
                listener.Stop()
        }

    let runController (ct: CancellationToken) =
        startMailboxProcessor ct (fun (inbox: MailboxProcessor<Msg>) ->
            let listeners = ResizeArray<_>()

            async {
                while not ct.IsCancellationRequested do
                    let! msg = inbox.Receive()
                    match msg with
                    | Connect (l, ns) ->
                        printfn "Connect: %A %A" l ns
                        listeners.Add(l)
                    | Disconnect l ->
                        Console.WriteLine "Disconnect"
                        listeners.Remove(l) |> ignore
                    | Tick msg -> listeners.ForEach(fun l -> l.Post msg)
            })



    let runRequestDispatcher () =
        let cts = new CancellationTokenSource()
        let token = cts.Token
        let controller = runController token
        let listener = TcpListener(IPAddress.Parse(ipAddress), port)

        try
            listener.Start(10)
        with
        | :? SocketException ->
            failwithf "ERROR: %s/%i is using by another program" ipAddress port
        | err ->
            failwithf "ERROR: %s" err.Message

        Async.Start(acceptLoop listener token controller, token)
        Async.Start(heartbeat 1000 token controller, token)

        { new IDisposable with
            member x.Dispose() = cts.Cancel() }

    let start () =
        use dispose = runRequestDispatcher ()
        printfn "press any key to stop..."
        Console.ReadKey() |> ignore

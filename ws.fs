namespace WebSocket

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading
open System.Runtime.Serialization
open System.Security.Cryptography

module ServerUtil =

  let guid6455 = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"

  let isWebSocketsUpgrade (lines: string array) =
    [| "GET /timer HTTP/1.1"; "Upgrade: websocket"; "Connection: Upgrade"|]
    |> Array.map(fun x->lines |> Array.exists(fun y->x.ToLower()=y.ToLower()))
    |> Array.reduce(fun x y->x && y)

  let calcWSAccept6455 (secWebSocketKey:string) =
    let ck = secWebSocketKey + guid6455
    let sha1 = SHA1CryptoServiceProvider.Create()
    let hashBytes = ck |> Encoding.ASCII.GetBytes |> sha1.ComputeHash
    let Sec_WebSocket_Accept = hashBytes |> Convert.ToBase64String
    Sec_WebSocket_Accept

  let createAcceptString6455 acceptCode =
    "HTTP/1.1 101 Switching Protocols\r\n" +
    "Upgrade: websocket\r\n" +
    "Connection: Upgrade\r\n" +
    "Sec-WebSocket-Accept: " + acceptCode + "\r\n" + "\r\n"

  let getKey (key:String) arr =
      try
        let item = (Array.find (fun (s:String) -> s.StartsWith(key)) arr)
        item.Substring key.Length
      with
        | _ -> ""

  let makeFrame_ShortTxt (P:byte array) =
    let message = new MemoryStream()
    try
      message.WriteByte(byte 0x81)
      message.WriteByte(byte P.Length)
      message.Write(P,0,P.Length)
      message.ToArray()
    finally
      message.Close()

open ServerUtil

[<DataContract>]
type Time =
    { [<DataMember(Name = "hour")>] mutable Hour : int
      [<DataMember(Name = "minute")>] mutable Minute : int
      [<DataMember(Name = "second")>] mutable Second : int }
    static member New(dt : DateTime) = {Hour = dt.Hour; Minute = dt.Minute; Second = dt.Second}

type Msg =
    | Connect of MailboxProcessor<Time> * NetworkStream
    | Disconnect of MailboxProcessor<Time>
    | Tick of Time

module WebSocketServer =

  let port = 1900
  let ipAddress = IPAddress.Loopback.ToString()
  let startMailboxProcessor ct f = MailboxProcessor.Start(f, cancellationToken = ct)

  let writeTime (ns: NetworkStream) (time: Time) = async {
      let json = System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof<Time>)
      let payload = new MemoryStream()
      json.WriteObject(payload, time)
      let df = makeFrame_ShortTxt <| payload.ToArray()
      do! ns.AsyncWrite(df,0,df.Length)
      }

  let runTelemetry (ns: NetworkStream) (inbox: MailboxProcessor<Time>)
      (ct: CancellationToken) (ctrl: MailboxProcessor<Msg>) = async {
      try while not ct.IsCancellationRequested do
          let! time = inbox.Receive()
          do! writeTime ns (Time.New(DateTime.Now))
      finally
          printfn "PUSHER DIE"
          ctrl.Post(Disconnect <| inbox)
          ns.Close()
      }

  let runLoop (ns: NetworkStream) (inbox: MailboxProcessor<Time>)
      (ct: CancellationToken) (tcp: TcpClient) (ctrl: MailboxProcessor<Msg>) = async {
      try while not ct.IsCancellationRequested do
          let bytes = Array.create tcp.ReceiveBufferSize (byte 0)
          let! len = ns.ReadAsync (bytes, 0, bytes.Length) |> Async.AwaitTask
          printfn "HANDLE FRAME %A" bytes.[1..len]
          do! writeTime ns (Time.New(DateTime.Now))
      finally
          printfn "LOOP DIE"
          ctrl.Post(Disconnect <| inbox)
          ns.Close()
      }


  let heartbeat (ctrl: MailboxProcessor<Msg>) (interval:int) = async {
      while true do
          do! Async.Sleep interval
          ctrl.Post(Tick <| Time.New(DateTime.Now))
      }

  let runController (ct: CancellationToken) =
      startMailboxProcessor ct (fun (inbox: MailboxProcessor<Msg>) ->
          let listeners = new ResizeArray<_>()
          async {
              while not ct.IsCancellationRequested do
                  let! msg = inbox.Receive()
                  match msg with
                  | Connect (l,ns) ->
                      printfn "Connect: %A %A" l ns
                      listeners.Add(l)
                  | Disconnect l ->
                      Console.WriteLine "Disconnect"
                      listeners.Remove(l) |> ignore
                  | Tick msg -> listeners.ForEach(fun l ->
                        l.Post msg)
          }
      )

  let runWorkers (tcp: TcpClient) (ctrl: MailboxProcessor<Msg>) ct =
      startMailboxProcessor ct (fun (inbox: MailboxProcessor<Time>) ->
          async {
              let ns = tcp.GetStream()
              let bytes = Array.create tcp.ReceiveBufferSize (byte 0)
              let! len = ns.ReadAsync (bytes, 0, bytes.Length) |> Async.AwaitTask
              if len > 8 then
                  let lines = bytes.[..(len-9)]
                              |> System.Text.UTF8Encoding.UTF8.GetString
                              |> fun hs->hs.Split([|"\r\n"|], StringSplitOptions.RemoveEmptyEntries)
                  match isWebSocketsUpgrade lines with
                  | true ->
                      let acceptStr = (getKey "Sec-WebSocket-Key:" lines).Substring(1)
                                      |> calcWSAccept6455
                                      |> createAcceptString6455
                      do! ns.AsyncWrite <| Encoding.ASCII.GetBytes acceptStr
                      ctrl.Post(Connect (inbox,ns))
                      Async.Start(runTelemetry ns inbox ct ctrl, ct)
                      Async.Start(runLoop ns inbox ct tcp ctrl, ct)
//                    return! runLoop ns inbox ct tcp ctrl
                  | _ ->
                      tcp.Close()
              else
                  tcp.Close()
          }
     )

  let acceptLoop (controller: MailboxProcessor<Msg>)
      (listener: TcpListener) (cts: CancellationToken) = async {
      try
          listener.Start(10)
          while not cts.IsCancellationRequested do
              let! client = Async.FromBeginEnd(listener.BeginAcceptTcpClient, listener.EndAcceptTcpClient)
              client.NoDelay <- true
              runWorkers client controller cts |> ignore
      finally
          listener.Stop()
      }

  let runRequestDispatcher () =
      let listener = new TcpListener(IPAddress.Parse(ipAddress), port)
      let cts = new CancellationTokenSource()
      let token = cts.Token
      let controller = runController token
      Async.Start(acceptLoop controller listener token, token)
      Async.Start(heartbeat controller 1000, token)
      { new IDisposable with member x.Dispose() = cts.Cancel()}

  let start() =
      let dispose = runRequestDispatcher ()
      printfn "press any key to stop..."
      Console.ReadKey() |> ignore
      dispose.Dispose()

module Program =

  [<EntryPoint>]
  let main argv =
      WebSocketServer.start()
      0

namespace N2O

open System.Net.WebSockets
open System.Collections.Specialized

// Most minimal type system for F# WebSocket server infrastructure

[<AutoOpen>]
module Types =

    type Msg =
        | Bin of byte array
        | Text of string
        | Nope

    type Sup =
        | Connect of MailboxProcessor<Msg> * WebSocket
        | Disconnect of MailboxProcessor<Msg>
        | Close of WebSocket
        | Tick

    type Req = 
        { path    : string;
          method  : string;
          version : string;
          headers : NameValueCollection }

    type Res =
        | Error of string
        | Reply of Msg
        | Ok

    type Proto<'ev> =
        { inh : Res; proto : Msg -> 'ev }

        member m.useless : 'ev -> Res = fun _ -> m.inh

    type Cx<'ev> =
        { req : Req; ctx : 'ev -> Res }

        member cx.run (m : Proto<'ev>) (msg : Msg)
               (handlers : (Cx<'ev> -> Cx<'ev>) list) =
            (List.fold (|>) cx handlers).ctx (m.proto msg)

    let mkHandler (m : Proto<'ev>) handlers : Req -> Msg -> Res =
        fun req msg -> { req = req; ctx = m.useless }.run m msg handlers

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

    type Proto<'ev>  = Msg -> 'ev
    type Router<'ev> = Req -> 'ev -> Res

    let mkHandler (proto : Proto<'ev>) router : Req -> Msg -> Res =
        fun req msg -> (router req (proto msg))

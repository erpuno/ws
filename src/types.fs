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

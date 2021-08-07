namespace N2O

open System.Net.Sockets
open System.Collections.Specialized

// The most minimal type system for WebSocket server infrastructure

[<AutoOpen>]
module Types =

    type Msg =
        | Bin of byte array
        | Text of string
        | Nope

    type Sup =
        | Connect of MailboxProcessor<Msg> * NetworkStream
        | Disconnect of MailboxProcessor<Msg>
        | Close of NetworkStream
        | Tick

    type Req =
        { path    : string;
          method  : string;
          version : string;
          headers : NameValueCollection }

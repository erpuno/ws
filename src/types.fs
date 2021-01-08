namespace N2O

open System
open System.Net.WebSockets

// Most minimal type system for F# WebSocket server infrastructure

[<AutoOpen>]
module Types =

    type Msg =
        | Bin of byte []
        | Text of string
        | Ping

    type Sup =
        | Connect of MailboxProcessor<Msg> * WebSocket
        | Disconnect of MailboxProcessor<Msg>
        | Close of WebSocket
        | Tick

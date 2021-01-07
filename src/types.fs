namespace N2O

open System
open System.Net.WebSockets

// Most minimal type system for F# WebSocket server infrastructure

[<AutoOpen>]
module Types =

    type Payload =
        | Bin of byte []
        | Ping

    type Sup =
        | Connect of MailboxProcessor<Payload> * WebSocket
        | Disconnect of MailboxProcessor<Payload>
        | Close of WebSocket
        | Tick

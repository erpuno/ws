namespace N2O

open System
open System.Net.Sockets

// Most minimal type system for F# WebSocket server infrastructure

[<AutoOpen>]
module Telemetry =

    type Payload =
        | Bin of byte []
        | Ping

    type Sup =
        | Connect of MailboxProcessor<Payload> * NetworkStream
        | Disconnect of MailboxProcessor<Payload>
        | Close of NetworkStream
        | Tick

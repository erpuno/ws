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

// Most minimal type system for F# WebSocket server infrastructure

[<AutoOpen>]
module Telemetry =

    [<DataContract>]
    type Payload =
         | Bin of byte[]
         | Ping

    type Sup =
         | Connect of MailboxProcessor<Payload> * WebSocket
         | Disconnect of MailboxProcessor<Payload>
         | Close of WebSocket
         | Tick

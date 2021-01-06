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

[<AutoOpen>]
module Telemetry =

    [<DataContract>]
    type Payload =
         | Bin of byte[]
         | Ping

    type Sup =
        | Connect of MailboxProcessor<Payload> * NetworkStream
        | Disconnect of MailboxProcessor<Payload>
        | Tick

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
    type Time =
        { [<DataMember(Name = "hour")>]
          mutable Hour: int
          [<DataMember(Name = "minute")>]
          mutable Minute: int
          [<DataMember(Name = "second")>]
          mutable Second: int }
        static member New(dt: DateTime) =
            { Hour = dt.Hour
              Minute = dt.Minute
              Second = dt.Second }

type Msg =
    | Connect of MailboxProcessor<Time> * NetworkStream
    | Disconnect of MailboxProcessor<Time>
    | Tick of Time

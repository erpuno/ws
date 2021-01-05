open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Net.WebSockets
open System.Text
open System.Threading
open System.Runtime.Serialization
open System.Security.Cryptography

open N2O

module Program =

    [<EntryPoint>]
    let main _ =
        let mutable exitCode = 0

        try
            Server.start ()
        with exn ->
            Console.WriteLine exn.Message
            exitCode <- 1

        exitCode

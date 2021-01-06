open N2O

module Program =

    [<EntryPoint>]
    let main _ =
        let mutable exitCode = 0
        let echo = fun x -> x
        try
            Stream.protocol <- echo
            use dispose = Server.start "0.0.0.0" 1900
            System.Threading.Thread.Sleep -1
        with exn ->
            printfn "EXIT: %A" exn.Message
            exitCode <- 1
        exitCode

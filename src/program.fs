open N2O

module Program =

    [<EntryPoint>]
    let main _ =
        let mutable (workers,io) = (4,4)
        let mutable ret = 0
        let echo = fun x -> x
        try
            System.Threading.ThreadPool.GetMinThreads(&workers,&io)
            printfn "N2O/F# WebSocket Server"
            printfn "[threads] Workers: %i, I/O: %i" workers io
            System.Threading.ThreadPool.SetMaxThreads(workers,io) |> ignore
            Stream.protocol <- echo
            use disposing = Server.start "0.0.0.0" 1900
            System.Threading.Thread.Sleep -1
        with exn ->
            printfn "EXIT: %A" exn.Message
            ret <- 1
        ret

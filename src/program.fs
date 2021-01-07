open N2O

module Program =

    [<EntryPoint>]
    let main _ =
        let mutable (cpu,io) = (4,4)
        let mutable ret = 0
        let echo = fun x -> x
        try
            System.Threading.ThreadPool.GetMinThreads(&cpu,&io)
            printfn "N2O/F# WebSocket Server 1.0"
            printfn "[smp] [cpu:%i] [io:%i]" cpu io
            System.Threading.ThreadPool.SetMaxThreads(cpu,io) |> ignore
            Stream.protocol <- echo
            use disposing = Server.start "0.0.0.0" 1900
            System.Threading.Thread.Sleep -1
        with exn ->
            printfn exn.Message
            ret <- 1
        ret

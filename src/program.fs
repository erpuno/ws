open N2O

// Echo sample

module Program =

    [<EntryPoint>]
    let main _ =
        let mutable port = 1900
        let mutable (cpu, io) = (4, 4)
        let mutable ret = 0

        let echoProto : Proto<Msg> = id
        let router : Router<Msg> = fun req msg -> Reply msg

        try
            System.Threading.ThreadPool.GetMinThreads(&cpu, &io)
            printfn "N2O/F# WebSocket Server 1.0"
            printfn "[smp] [cpu:%i] [io:%i]" cpu io
            System.Threading.ThreadPool.SetMaxThreads(cpu, io) |> ignore

            Stream.protocol <- mkHandler echoProto router
            use ws = Server.start "0.0.0.0" port
            System.Threading.Thread.Sleep -1
        with exn ->
            printfn "EXIT: %s" exn.Message
            ret <- 1
        ret

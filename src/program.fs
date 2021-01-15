open N2O

module Program =

    [<EntryPoint>]
    let main _ =
        let mutable port = 1900
        let mutable (cpu, io) = (4, 4)
        let mutable ret = 0

        let nope : Msg -> Msg = fun _ -> Nope
        let tick : Msg -> Msg = fun _ -> Text "TICK"
        let echo : Msg -> Msg = id
        let router : Req -> Msg -> Msg =
            fun x ->
                match x.path with
                | "/nope" -> nope
                | "/tick" -> tick
                | "/echo" -> echo
                | _ -> id

        try
            System.Threading.ThreadPool.GetMinThreads(&cpu, &io)
            printfn "N2O/F# WebSocket Server 1.0"
            printfn "[smp] [cpu:%i] [io:%i]" cpu io
            System.Threading.ThreadPool.SetMaxThreads(cpu, io) |> ignore

            Server.proto <- router
            use ws = Server.start "0.0.0.0" port
            System.Threading.Thread.Sleep -1
        with exn ->
            printfn "EXIT: %s" exn.Message
            ret <- 1
        ret

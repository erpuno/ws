open N2O

module Program =

    [<EntryPoint>]
    let main _ =
        let mutable port = 1900
        let mutable (cpu, io) = (4, 4)
        let mutable ret = 0

        let nope = fun _ -> Nope
        let tick = fun _ -> Text "TICK"
        let echo = id
        let router (req : Req) : Msg -> Msg =
            match req.path with
            | "/nope" -> nope
            | "/tick" -> tick
            | "/echo" -> echo
            | _ -> id

        try
            System.Threading.ThreadPool.GetMinThreads(&cpu, &io)
            printfn "[smp] [cpu:%i] [io:%i]" cpu io
            System.Threading.ThreadPool.SetMaxThreads(cpu, io) |> ignore

            Server.proto <- router
            use ws = Server.start "0.0.0.0" port
            System.Threading.Thread.Sleep -1
        with exn ->
            printfn "EXIT: %s" exn.Message
            ret <- 1
        ret
